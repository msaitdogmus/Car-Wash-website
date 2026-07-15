# 🚙 DryCar Care

Kırşehir'deki araç yıkama ve detaylı temizlik hizmetlerini dijitalleştiren, müşterilerin sıra beklemeden randevu alabildiği bir web uygulaması.

<p align="center">
  <a href="https://drycarkirsehir.com.tr/"><strong>Canlı Siteyi Görüntüle</strong></a>
</p>

![DryCar Care ana sayfa](docs/screenshots/anasayfa-hero.png)

## Proje hakkında

DryCar Care'i yalnızca bir tanıtım sitesi olarak değil, işletmenin günlük randevu akışını yöneten gerçek bir sistem olarak geliştirdik. Ziyaretçiler hizmetleri ve fiyatları inceleyebiliyor, uygun tarih ve saati seçerek randevu oluşturabiliyor. Müşteriler kendi randevularını yönetirken yönetici tarafında hizmet, kullanıcı ve randevu işlemleri tek panelden yürütülebiliyor.

### Öne çıkan özellikler

- Tarih, hizmet ve doluluk durumuna göre online randevu oluşturma
- Müşteriye özel yaklaşan/geçmiş randevular, düzenleme ve iptal işlemleri
- Yönetici adına randevu ekleme, randevu onaylama ve geçmiş kayıt yönetimi
- Hizmet ve fiyatlar için ekleme, güncelleme ve silme işlemleri
- Canlı Kırşehir hava durumu, yerel haberler, harita ve Instagram içerikleri
- Masaüstü ve mobil cihazlara uyumlu, sade arayüz
- Gmail OAuth altyapısıyla e-posta iletişimi ve Python destekli yüz doğrulama modülü

## Kullandığımız teknolojiler

| Katman | Teknolojiler |
| --- | --- |
| Backend | C#, .NET 8, ASP.NET Core, Razor Pages / MVC |
| Veri | Entity Framework Core, Microsoft SQL Server |
| Frontend | Razor, HTML5, CSS3, Bootstrap, JavaScript |
| Kimlik ve iletişim | Cookie tabanlı oturum, BCrypt, Gmail API / OAuth 2.0 |
| Entegrasyonlar | Open-Meteo, Google Maps, Instagram, yerel haber akışı |
| Altyapı | Kestrel, Cloudflare Tunnel, Linux systemd |

## Güvenlikte önem verdiğimiz noktalar

Güvenliği yalnızca giriş ekranından ibaret görmedik. Formlarda ASP.NET Core antiforgery tokenlarıyla CSRF koruması kullandık; müşteri ve yönetici işlemlerini yetkilendirme kurallarıyla ayırdık. Parolalar BCrypt ile tek yönlü olarak saklanıyor. Veritabanı ve Gmail gibi gizli bilgiler kaynak koddan ayrılarak yalnızca sunucunun okuyabildiği ortam değişkenlerinde tutuluyor.

Dış kaynaklardan gelen haber, bağlantı ve hava durumu verilerini doğrudan sayfaya basmak yerine doğrulayıp kaçışlayarak DOM tabanlı XSS riskini azalttık. Uygulama internete doğrudan port açmak yerine yerel Kestrel servisi ve Cloudflare tüneli üzerinden yayınlanıyor.

### Kaynak kod ve gizlilik

Bu herkese açık depoda üretimde çalışan kodların tamamı paylaşılmıyor. Projenin yapısını gösterecek kadar, sadeleştirilmiş ve güvenli örnekler bulunuyor; devamındaki iş kuralları ve üretim ayrıntıları gizlilik nedeniyle özel tutuluyor.

Depoda gerçek parola, API anahtarı, OAuth belirteci, veritabanı bağlantı bilgisi, müşteri kaydı veya biyometrik veri yer almaz. Paylaşılan örneklerde de gerçek ortam değerleri kullanılmaz. İncelemek isteyenler için seçilmiş parçaları **[kod örnekleri](docs/KOD_ORNEKLERI.md)** sayfasında topladık. Güvenlik yaklaşımının ayrıntıları ayrıca **[SECURITY.md](SECURITY.md)** dosyasında bulunuyor.

## Teknik olarak doğru yaptığımız şeyler

- Müşteri ve yönetici akışlarını birbirinden ayıran rol bazlı yapı
- Dolu saatleri sunucu tarafında kontrol eden randevu mantığı
- Veritabanı erişimini Entity Framework Core üzerinden yönetme
- Gizli anahtarları repoya koymama ve çalışma ortamından yükleme
- Mobil taşma kontrolleri, erişilebilir form etiketleri ve azaltılmış hareket desteği
- Servisi systemd ile izlenebilir ve yeniden başlatılabilir biçimde çalıştırma
- Üretim kaynaklarıyla tanıtım kodlarını birbirinden ayırma

## Ekran görüntüleri

### Ana sayfa ve kurumsal alanlar

| Canlı bilgi paneli ve çalışmalar | Hakkımızda |
| --- | --- |
| ![Canlı bilgi paneli](docs/screenshots/anasayfa-canli-panel.png) | ![Hakkımızda sayfası](docs/screenshots/hakkimizda.png) |

![Konum ve iletişim bilgileri](docs/screenshots/konum-ve-iletisim.png)

### Müşteri randevu deneyimi

| Randevu oluşturma | Randevularım |
| --- | --- |
| ![Randevu oluşturma ekranı](docs/screenshots/randevu-al.png) | ![Müşteri randevuları](docs/screenshots/randevularim.png) |

### Yönetim paneli

| Müşteri adına randevu | Hizmet yönetimi |
| --- | --- |
| ![Admin randevu ekleme](docs/screenshots/admin-randevu-ekle.png) | ![Admin hizmet yönetimi](docs/screenshots/admin-hizmetler.png) |

![Geçmiş randevuların yönetimi](docs/screenshots/admin-gecmis-randevular.png)

---

Bu depo şu anda DryCar Care'in proje tanıtımını ve ürün ekranlarını içerir. Canlı uygulama: **[drycarkirsehir.com.tr](https://drycarkirsehir.com.tr/)**
