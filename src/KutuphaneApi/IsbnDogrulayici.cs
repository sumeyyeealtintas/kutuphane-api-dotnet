namespace KutuphaneApi;

/// <summary>
/// ISBN-10 ve ISBN-13 numaralarını biçim ve sağlama toplamı (check digit) açısından doğrular.
/// Sağlama toplamı, yanlış yazılmış numaraların büyük kısmını yakalar.
/// </summary>
public static class IsbnDogrulayici
{
    /// <summary>Tire ve boşlukları temizler, X harfini büyütür. "978-0-306-40615-7" -> "9780306406157".</summary>
    public static string Normallestir(string isbn) =>
        new string(isbn.Where(c => c != '-' && !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    public static bool Gecerli(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return false;
        var n = Normallestir(isbn);
        return n.Length switch
        {
            10 => Isbn10(n),
            13 => Isbn13(n),
            _ => false
        };
    }

    // ISBN-10: sağdan sola ağırlıklar 1..10 (soldan 10..1); toplam 11'e tam bölünmeli. Son hane 'X' = 10.
    private static bool Isbn10(string n)
    {
        int toplam = 0;
        for (int i = 0; i < 10; i++)
        {
            int deger;
            if (i == 9 && n[i] == 'X') deger = 10;
            else if (char.IsAsciiDigit(n[i])) deger = n[i] - '0';
            else return false;
            toplam += deger * (10 - i);
        }
        return toplam % 11 == 0;
    }

    // ISBN-13: haneler sırayla 1 ve 3 ile çarpılır; toplam 10'a tam bölünmeli.
    private static bool Isbn13(string n)
    {
        if (!n.All(char.IsAsciiDigit)) return false;
        int toplam = 0;
        for (int i = 0; i < 13; i++)
            toplam += (n[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return toplam % 10 == 0;
    }
}
