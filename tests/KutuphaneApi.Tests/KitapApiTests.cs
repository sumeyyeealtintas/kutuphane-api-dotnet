using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KutuphaneApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KutuphaneApi.Tests;

/// <summary>Uçtan uca (entegrasyon) testleri: gerçek HTTP hattı, her test sınıfı örneği kendi bellek deposunu kullanır.</summary>
public class KitapApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _fabrika;
    public KitapApiTests(WebApplicationFactory<Program> fabrika) => _fabrika = fabrika;

    // Her test kendi temiz istemcisini ister: Production ortamı = örnek veri yüklenmez
    private HttpClient YeniIstemci() =>
        _fabrika.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

    private static KitapOlustur Ornek(string isbn = "9780306406157", string baslik = "Test Kitabı", string yazar = "Yazar") =>
        new(baslik, yazar, isbn, 2020);

    private static async Task<Kitap> KitapOku(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<Kitap>())!;

    [Fact]
    public async Task Saglik_Ok_Doner()
    {
        var r = await YeniIstemci().GetAsync("/saglik");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Olustur_201_Ve_Location_Basligi_Doner()
    {
        var c = YeniIstemci();
        var r = await c.PostAsJsonAsync("/api/kitaplar", Ornek());
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var kitap = await KitapOku(r);
        Assert.Equal($"/api/kitaplar/{kitap.Id}", r.Headers.Location!.ToString());
        Assert.Equal("9780306406157", kitap.Isbn);
        Assert.False(kitap.Odunc);

        var getir = await c.GetAsync(r.Headers.Location);
        Assert.Equal("Test Kitabı", (await KitapOku(getir)).Baslik);
    }

    [Fact]
    public async Task Tireli_Isbn_Normallestirilerek_Saklanir()
    {
        var r = await YeniIstemci().PostAsJsonAsync("/api/kitaplar", Ornek("978-0-306-40615-7"));
        Assert.Equal("9780306406157", (await KitapOku(r)).Isbn);
    }

    [Fact]
    public async Task Gecersiz_Girdi_400_Ve_Alan_Bazli_Hata_Doner()
    {
        var r = await YeniIstemci().PostAsJsonAsync("/api/kitaplar",
            new KitapOlustur("", null, "9780306406158", 1000));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var hatalar = json.RootElement.GetProperty("errors");
        foreach (var alan in new[] { "baslik", "yazar", "isbn", "yayinYili" })
            Assert.True(hatalar.TryGetProperty(alan, out _), $"'{alan}' için hata bekleniyordu");
    }

    [Fact]
    public async Task Ayni_Isbn_Ikinci_Kez_409_Doner()
    {
        var c = YeniIstemci();
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/kitaplar", Ornek())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/kitaplar", Ornek("978-0-306-40615-7"))).StatusCode);
    }

    [Fact]
    public async Task Olmayan_Kitap_404_Doner()
    {
        var c = YeniIstemci();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/kitaplar/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync("/api/kitaplar/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/kitaplar/999/odunc", null)).StatusCode);
    }

    [Fact]
    public async Task Patch_Yalnizca_Gonderilen_Alanlari_Degistirir()
    {
        var c = YeniIstemci();
        var kitap = await KitapOku(await c.PostAsJsonAsync("/api/kitaplar", Ornek()));

        var r = await c.PatchAsJsonAsync($"/api/kitaplar/{kitap.Id}", new KitapGuncelle("Yeni Başlık", null, null));
        var guncel = await KitapOku(r);
        Assert.Equal("Yeni Başlık", guncel.Baslik);
        Assert.Equal(kitap.Yazar, guncel.Yazar);          // dokunulmadı
        Assert.Equal(kitap.Isbn, guncel.Isbn);            // ISBN değiştirilemez
    }

    [Fact]
    public async Task Patch_Gecersiz_Yil_400_Doner()
    {
        var c = YeniIstemci();
        var kitap = await KitapOku(await c.PostAsJsonAsync("/api/kitaplar", Ornek()));
        var r = await c.PatchAsJsonAsync($"/api/kitaplar/{kitap.Id}", new KitapGuncelle(null, null, 1200));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Sil_204_Sonra_404_Doner()
    {
        var c = YeniIstemci();
        var kitap = await KitapOku(await c.PostAsJsonAsync("/api/kitaplar", Ornek()));
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/kitaplar/{kitap.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/kitaplar/{kitap.Id}")).StatusCode);
    }

    [Fact]
    public async Task Odunc_Ve_Iade_Akisi_Ve_Cakismalar()
    {
        var c = YeniIstemci();
        var id = (await KitapOku(await c.PostAsJsonAsync("/api/kitaplar", Ornek()))).Id;

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/kitaplar/{id}/iade", null)).StatusCode);   // zaten kütüphanede

        var odunc = await c.PostAsync($"/api/kitaplar/{id}/odunc", null);
        Assert.Equal(HttpStatusCode.OK, odunc.StatusCode);
        Assert.True((await KitapOku(odunc)).Odunc);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/kitaplar/{id}/odunc", null)).StatusCode); // zaten ödünçte

        var iade = await c.PostAsync($"/api/kitaplar/{id}/iade", null);
        Assert.False((await KitapOku(iade)).Odunc);
    }

    [Fact]
    public async Task Liste_Arama_Filtre_Ve_Sayfalama()
    {
        var c = YeniIstemci();
        await c.PostAsJsonAsync("/api/kitaplar", Ornek("9780306406157", "Işık Hızı", "Ayşe Yılmaz"));
        await c.PostAsJsonAsync("/api/kitaplar", Ornek("9780132350884", "Temiz Kod", "Robert Martin"));
        await c.PostAsJsonAsync("/api/kitaplar", Ornek("0306406152", "Algoritmalar", "Ayşe Demir"));

        var tumu = (await c.GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar"))!;
        Assert.Equal(3, tumu.Toplam);

        // Türkçe büyük/küçük harf: "ışık" araması "Işık"ı bulmalı
        var arama = (await c.GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar?arama=ışık"))!;
        Assert.Single(arama.Ogeler);

        var yazar = (await c.GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar?arama=ayşe"))!;
        Assert.Equal(2, yazar.Toplam);

        var sayfa = (await c.GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar?sayfa=2&boyut=2"))!;
        Assert.Single(sayfa.Ogeler);
        Assert.Equal(3, sayfa.Toplam);
    }

    [Fact]
    public async Task Odunc_Filtresi_Calisir()
    {
        var c = YeniIstemci();
        var a = await KitapOku(await c.PostAsJsonAsync("/api/kitaplar", Ornek("9780306406157")));
        await c.PostAsJsonAsync("/api/kitaplar", Ornek("9780132350884"));
        await c.PostAsync($"/api/kitaplar/{a.Id}/odunc", null);

        var oduncte = (await c.GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar?odunc=true"))!;
        Assert.Single(oduncte.Ogeler);
        Assert.Equal(a.Id, oduncte.Ogeler[0].Id);
    }

    [Fact]
    public async Task Gecersiz_Sayfa_Parametreleri_Sinirlanir()
    {
        var s = (await YeniIstemci().GetFromJsonAsync<SayfaliSonuc<Kitap>>("/api/kitaplar?sayfa=-5&boyut=100000"))!;
        Assert.Equal(1, s.Sayfa);
        Assert.Equal(100, s.Boyut);
    }

    [Fact]
    public async Task Es_Zamanli_Eklemeler_Cakisma_Uretmez()
    {
        // Aynı ISBN'i 20 istek aynı anda eklemeye çalışır: tam olarak biri 201, kalanı 409 olmalı
        var c = YeniIstemci();
        var yanitlar = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => c.PostAsJsonAsync("/api/kitaplar", Ornek())));
        Assert.Equal(1, yanitlar.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, yanitlar.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}
