using KutuphaneApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IKitapDeposu, BellekKitapDeposu>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                      // /openapi/v1.json
    var depo = app.Services.GetRequiredService<IKitapDeposu>();
    foreach (var (baslik, yazar, isbn, yil) in new[]
    {
        // Sağlama toplamı geçerli örnek ISBN'ler (yalnızca geliştirme ortamında, tanıtım amaçlı)
        ("Clean Code", "Robert C. Martin", "9780132350884", 2008),
        ("Örnek Roman", "Örnek Yazar", "9780306406157", 2020),
        ("Örnek Ansiklopedi", "Örnek Editör", "0306406152", 1999),
    })
        depo.Ekle(new KitapOlustur(baslik, yazar, isbn, yil));
}

app.MapGet("/saglik", () => Results.Ok(new { durum = "ok" }));

var kitaplar = app.MapGroup("/api/kitaplar");

kitaplar.MapGet("/", (IKitapDeposu depo, string? arama, bool? odunc, int? sayfa, int? boyut) =>
{
    var s = Math.Max(1, sayfa ?? 1);
    var b = Math.Clamp(boyut ?? 10, 1, 100);
    return Results.Ok(depo.Listele(arama, odunc, s, b));
});

kitaplar.MapGet("/{id:int}", (int id, IKitapDeposu depo) =>
    depo.Getir(id) is { } kitap ? Results.Ok(kitap) : Results.NotFound(new { hata = "Kitap bulunamadı" }));

kitaplar.MapPost("/", (KitapOlustur istek, IKitapDeposu depo) =>
{
    var hatalar = Dogrulama.Dogrula(istek);
    if (hatalar.Count > 0) return Results.ValidationProblem(hatalar);

    var (kitap, cakisma) = depo.Ekle(istek);
    if (cakisma) return Results.Conflict(new { hata = "Bu ISBN ile kayıtlı bir kitap zaten var" });
    return Results.Created($"/api/kitaplar/{kitap!.Id}", kitap);
});

kitaplar.MapPatch("/{id:int}", (int id, KitapGuncelle istek, IKitapDeposu depo) =>
{
    var hatalar = Dogrulama.Dogrula(istek);
    if (hatalar.Count > 0) return Results.ValidationProblem(hatalar);
    return depo.Guncelle(id, istek) is { } kitap ? Results.Ok(kitap) : Results.NotFound(new { hata = "Kitap bulunamadı" });
});

kitaplar.MapDelete("/{id:int}", (int id, IKitapDeposu depo) =>
    depo.Sil(id) ? Results.NoContent() : Results.NotFound(new { hata = "Kitap bulunamadı" }));

kitaplar.MapPost("/{id:int}/odunc", (int id, IKitapDeposu depo) => Sonuc(depo.OduncVer(id), id, depo, "Kitap zaten ödünçte"));
kitaplar.MapPost("/{id:int}/iade", (int id, IKitapDeposu depo) => Sonuc(depo.IadeAl(id), id, depo, "Kitap zaten kütüphanede"));

app.Run();

static IResult Sonuc(OduncSonucu s, int id, IKitapDeposu depo, string cakismaMesaji) => s switch
{
    OduncSonucu.Basarili => Results.Ok(depo.Getir(id)),
    OduncSonucu.Bulunamadi => Results.NotFound(new { hata = "Kitap bulunamadı" }),
    _ => Results.Conflict(new { hata = cakismaMesaji })
};

// WebApplicationFactory<Program> ile entegrasyon testi yazabilmek için gerekli
public partial class Program { }
