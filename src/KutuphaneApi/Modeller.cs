namespace KutuphaneApi;

/// <summary>Kütüphanedeki bir kitap.</summary>
public record Kitap(int Id, string Baslik, string Yazar, string Isbn, int YayinYili, bool Odunc);

/// <summary>Yeni kitap eklerken istemcinin gönderdiği veri.</summary>
public record KitapOlustur(string? Baslik, string? Yazar, string? Isbn, int YayinYili);

/// <summary>Kısmi güncelleme (PATCH): yalnızca gönderilen alanlar değişir. ISBN değiştirilemez.</summary>
public record KitapGuncelle(string? Baslik, string? Yazar, int? YayinYili);

public record SayfaliSonuc<T>(IReadOnlyList<T> Ogeler, int Toplam, int Sayfa, int Boyut);
