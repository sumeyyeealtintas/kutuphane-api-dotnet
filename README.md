# 📚 Kütüphane API (ASP.NET Core)

**C# / ASP.NET Core Minimal API** ile yazılmış kitap yönetimi servisi. Kitap ekleme, arama, güncelleme, silme ve **ödünç verme / iade akışı** içerir. ISBN numaralarının **sağlama toplamı (check digit)** doğrulanır.

## 🧰 Teknolojiler
C# 14 · .NET 10 · ASP.NET Core Minimal API · OpenAPI · xUnit · `WebApplicationFactory` (entegrasyon testi) · GitHub Actions

## ✨ Öne Çıkanlar
- **ISBN-10 ve ISBN-13 sağlama toplamı doğrulaması.** Tire/boşluk temizlenir (`978-0-306-40615-7`), ISBN-10'daki `X` hanesi desteklenir. ISBN-13'te tek hane hatasının her zaman yakalandığı testle kanıtlanır
- **Doğru HTTP anlamı:** `201 Created` + `Location` başlığı, `204`, `400` (alan bazlı hata), `404`, `409 Conflict` (aynı ISBN ya da zaten ödünçte olan kitabı tekrar ödünç verme)
- **Kısmi güncelleme (`PATCH`):** yalnızca gönderilen alanlar değişir, ISBN değiştirilemez
- **Türkçe uyumlu arama:** `ışık` araması `Işık`'ı bulur (`tr-TR` kültür kuralları)
- **Eşzamanlılık güvenliği:** aynı ISBN'i aynı anda ekleyen 20 istekten tam olarak biri başarılır (testle doğrulanır)
- **Bağımlılık enjeksiyonu:** depo bir arayüzün (`IKitapDeposu`) arkasındadır; bellek içi depo yerine EF Core + veritabanı, API koduna dokunmadan takılabilir
- Sayfalama sınırlandırılır (`boyut` en fazla 100), geçersiz parametreler çökmez

## 🔌 Uç Noktalar
| Metot | Yol | Açıklama | Başarı / Hata kodları |
|---|---|---|---|
| GET | `/saglik` | Servis sağlık kontrolü | 200 |
| GET | `/api/kitaplar?arama=&odunc=&sayfa=&boyut=` | Listele, ara, filtrele | 200 |
| GET | `/api/kitaplar/{id}` | Tek kitap | 200 / 404 |
| POST | `/api/kitaplar` | Yeni kitap | 201 / 400 / 409 |
| PATCH | `/api/kitaplar/{id}` | Kısmi güncelleme | 200 / 400 / 404 |
| DELETE | `/api/kitaplar/{id}` | Sil | 204 / 404 |
| POST | `/api/kitaplar/{id}/odunc` | Ödünç ver | 200 / 404 / 409 |
| POST | `/api/kitaplar/{id}/iade` | İade al | 200 / 404 / 409 |

Geliştirme ortamında OpenAPI dokümanı `/openapi/v1.json` adresindedir, örnek istekler `src/KutuphaneApi/KutuphaneApi.http` dosyasındadır.

Örnek hatalı istek:
```bash
curl -X POST http://localhost:5000/api/kitaplar -H "Content-Type: application/json" \
  -d '{"baslik":"","yazar":"Ali","isbn":"9780306406158","yayinYili":1000}'
```
```json
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "baslik": ["Başlık zorunludur"],
              "isbn": ["Geçerli bir ISBN-10 veya ISBN-13 giriniz (sağlama toplamı tutmuyor ya da biçim hatalı)"],
              "yayinYili": ["Yayın yılı 1450 ile 2027 arasında olmalı"] } }
```

## 🚀 Çalıştırma
Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download)
```bash
dotnet run --project src/KutuphaneApi --urls http://localhost:5000
dotnet test        # 31 test
```
Geliştirme ortamında 3 örnek kitap yüklenir.

## 🧪 Test
31 test: ISBN doğrulama (geçerli/geçersiz numaralar, tek hane hatası taraması), tüm uç noktaların uçtan uca testleri, arama/filtre/sayfalama, ödünç akışı ve çakışmaları, eşzamanlı ekleme. Her test kendi temiz uygulama örneğini kullanır (testler birbirini etkilemez).

## 🔒 Güvenlik Notu
.NET şablonunun getirdiği `Microsoft.OpenApi 2.0.0` bağımlılığında yüksek önem dereceli bilinen bir açık (GHSA-v5pm-xwqc-g5wc) vardı; paket yamalı **2.7.5** sürümüne sabitlendi. CI, her çalışmada `dotnet list package --vulnerable` ile bağımlılıkları tarar.

## 📁 Yapı
```
├── src/KutuphaneApi/
│   ├── Program.cs           # uç noktalar
│   ├── KitapDeposu.cs       # IKitapDeposu + bellek içi, thread-safe uygulama
│   ├── IsbnDogrulayici.cs   # ISBN-10/13 sağlama toplamı
│   ├── Dogrulama.cs         # girdi doğrulama (400 yanıtları)
│   └── Modeller.cs          # record'lar
└── tests/KutuphaneApi.Tests/
```

## 🔭 Geliştirme Fikirleri
- EF Core + PostgreSQL ile kalıcı depo (arayüz sayesinde API değişmez)
- JWT ile kimlik doğrulama, yalnızca yöneticinin silebilmesi
- Ödünç geçmişi ve iade tarihi
- Dockerfile ve docker-compose
