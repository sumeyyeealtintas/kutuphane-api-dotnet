namespace KutuphaneApi;

public enum OduncSonucu { Basarili, Bulunamadi, Cakisma }

public interface IKitapDeposu
{
    (Kitap? Kitap, bool IsbnCakismasi) Ekle(KitapOlustur istek);
    Kitap? Getir(int id);
    SayfaliSonuc<Kitap> Listele(string? arama, bool? odunc, int sayfa, int boyut);
    Kitap? Guncelle(int id, KitapGuncelle istek);
    bool Sil(int id);
    OduncSonucu OduncVer(int id);
    OduncSonucu IadeAl(int id);
}

/// <summary>
/// Bellek içi, iş parçacığı güvenli (thread-safe) depo. Gerçek bir uygulamada bu arayüzün arkasına
/// bir veritabanı (EF Core) konur; API kodu değişmez (bağımlılık enjeksiyonunun faydası).
/// </summary>
public class BellekKitapDeposu : IKitapDeposu
{
    private readonly Dictionary<int, Kitap> _kitaplar = new();
    private readonly object _kilit = new();
    private int _sonId;

    public (Kitap? Kitap, bool IsbnCakismasi) Ekle(KitapOlustur istek)
    {
        var isbn = IsbnDogrulayici.Normallestir(istek.Isbn!);
        lock (_kilit)
        {
            if (_kitaplar.Values.Any(k => k.Isbn == isbn)) return (null, true);
            var kitap = new Kitap(++_sonId, istek.Baslik!.Trim(), istek.Yazar!.Trim(), isbn, istek.YayinYili, false);
            _kitaplar[kitap.Id] = kitap;
            return (kitap, false);
        }
    }

    public Kitap? Getir(int id)
    {
        lock (_kilit) return _kitaplar.GetValueOrDefault(id);
    }

    public SayfaliSonuc<Kitap> Listele(string? arama, bool? odunc, int sayfa, int boyut)
    {
        lock (_kilit)
        {
            IEnumerable<Kitap> sorgu = _kitaplar.Values;
            if (!string.IsNullOrWhiteSpace(arama))
            {
                // Türkçe kültür kuralları: "I/ı" ve "İ/i" doğru eşleşsin
                var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
                sorgu = sorgu.Where(k => tr.CompareInfo.IndexOf(k.Baslik, arama, System.Globalization.CompareOptions.IgnoreCase) >= 0
                                      || tr.CompareInfo.IndexOf(k.Yazar, arama, System.Globalization.CompareOptions.IgnoreCase) >= 0);
            }
            if (odunc is not null) sorgu = sorgu.Where(k => k.Odunc == odunc);

            var tumu = sorgu.OrderBy(k => k.Id).ToList();
            var ogeler = tumu.Skip((sayfa - 1) * boyut).Take(boyut).ToList();
            return new SayfaliSonuc<Kitap>(ogeler, tumu.Count, sayfa, boyut);
        }
    }

    public Kitap? Guncelle(int id, KitapGuncelle istek)
    {
        lock (_kilit)
        {
            if (!_kitaplar.TryGetValue(id, out var mevcut)) return null;
            var yeni = mevcut with
            {
                Baslik = string.IsNullOrWhiteSpace(istek.Baslik) ? mevcut.Baslik : istek.Baslik.Trim(),
                Yazar = string.IsNullOrWhiteSpace(istek.Yazar) ? mevcut.Yazar : istek.Yazar.Trim(),
                YayinYili = istek.YayinYili ?? mevcut.YayinYili
            };
            _kitaplar[id] = yeni;
            return yeni;
        }
    }

    public bool Sil(int id)
    {
        lock (_kilit) return _kitaplar.Remove(id);
    }

    public OduncSonucu OduncVer(int id) => DurumDegistir(id, hedefOdunc: true);
    public OduncSonucu IadeAl(int id) => DurumDegistir(id, hedefOdunc: false);

    private OduncSonucu DurumDegistir(int id, bool hedefOdunc)
    {
        lock (_kilit)
        {
            if (!_kitaplar.TryGetValue(id, out var k)) return OduncSonucu.Bulunamadi;
            if (k.Odunc == hedefOdunc) return OduncSonucu.Cakisma;   // zaten ödünçte / zaten iade edilmiş
            _kitaplar[id] = k with { Odunc = hedefOdunc };
            return OduncSonucu.Basarili;
        }
    }
}
