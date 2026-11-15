using KutuphaneApi;

namespace KutuphaneApi.Tests;

public class IsbnDogrulayiciTests
{
    [Theory]
    [InlineData("9780306406157")]          // ISBN-13 (Vikipedi örneği)
    [InlineData("978-0-306-40615-7")]      // tireli
    [InlineData("978 0 306 40615 7")]      // boşluklu
    [InlineData("0306406152")]             // ISBN-10
    [InlineData("080442957X")]             // ISBN-10, son hane X (=10)
    [InlineData("080442957x")]             // küçük harf x
    [InlineData("9780132350884")]
    public void Gecerli_Numaralar_Kabul_Edilir(string isbn) =>
        Assert.True(IsbnDogrulayici.Gecerli(isbn));

    [Theory]
    [InlineData("9780306406158")]          // son hane yanlış (sağlama toplamı tutmuyor)
    [InlineData("0306406153")]
    [InlineData("12345")]                  // çok kısa
    [InlineData("97803064061571")]         // çok uzun
    [InlineData("97803064O6157")]          // O harfi (sıfır değil)
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Gecersiz_Numaralar_Reddedilir(string? isbn) =>
        Assert.False(IsbnDogrulayici.Gecerli(isbn));

    [Fact]
    public void Tek_Hane_Hatasi_Her_Zaman_Yakalanir()
    {
        // ISBN-13'te tek bir hanenin değişmesi sağlama toplamını DAİMA bozar (10'a bölünebilirlik)
        const string dogru = "9780306406157";
        for (int i = 0; i < 13; i++)
        for (char c = '0'; c <= '9'; c++)
        {
            if (c == dogru[i]) continue;
            var bozuk = dogru.Remove(i, 1).Insert(i, c.ToString());
            Assert.False(IsbnDogrulayici.Gecerli(bozuk), $"{bozuk} yanlışlıkla geçerli sayıldı");
        }
    }

    [Fact]
    public void Normallestir_Tire_Ve_Bosluklari_Temizler() =>
        Assert.Equal("9780306406157", IsbnDogrulayici.Normallestir(" 978-0 306-40615-7 "));
}
