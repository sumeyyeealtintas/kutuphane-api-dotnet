namespace KutuphaneApi;

/// <summary>Girdi doğrulama. Dönen sözlük, RFC 7807 "ValidationProblem" yanıtına dönüştürülür (HTTP 400).</summary>
public static class Dogrulama
{
    public static Dictionary<string, string[]> Dogrula(KitapOlustur k)
    {
        var h = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(k.Baslik)) h["baslik"] = ["Başlık zorunludur"];
        else if (k.Baslik.Length > 200) h["baslik"] = ["Başlık en fazla 200 karakter olabilir"];
        if (string.IsNullOrWhiteSpace(k.Yazar)) h["yazar"] = ["Yazar zorunludur"];
        if (!IsbnDogrulayici.Gecerli(k.Isbn)) h["isbn"] = ["Geçerli bir ISBN-10 veya ISBN-13 giriniz (sağlama toplamı tutmuyor ya da biçim hatalı)"];
        YilKontrol(k.YayinYili, h);
        return h;
    }

    public static Dictionary<string, string[]> Dogrula(KitapGuncelle k)
    {
        var h = new Dictionary<string, string[]>();
        if (k.Baslik is { Length: > 200 }) h["baslik"] = ["Başlık en fazla 200 karakter olabilir"];
        if (k.YayinYili is { } y) YilKontrol(y, h);
        return h;
    }

    private static void YilKontrol(int yil, Dictionary<string, string[]> h)
    {
        if (yil < 1450 || yil > DateTime.UtcNow.Year + 1)
            h["yayinYili"] = [$"Yayın yılı 1450 ile {DateTime.UtcNow.Year + 1} arasında olmalı"];
    }
}
