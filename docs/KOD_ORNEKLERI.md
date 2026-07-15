# Kod okuma rehberi

Repo artık yalnız sadeleştirilmiş örnekler değil, uygulamanın güvenli açık kaynak sürümünü içeriyor. Aşağıdaki sıra kodu daha rahat takip etmeyi sağlar.

## 1. Uygulama başlangıcı

- [`Program.cs`](../src/DryCar/Program.cs): Kestrel ve adlandırılmış `HttpClient` ayarları
- [`Startup.cs`](../src/DryCar/Startup.cs): bağımlılık kaydı, SQL Server, oturum, CSRF, Data Protection ve middleware sırası

İlk olarak bu iki dosyayı okuyunca uygulamadaki servislerin nasıl bir araya geldiği görülür.

## 2. Veri modeli

- [`ApplicationDbContext.cs`](../src/DryCar/Data/ApplicationDbContext.cs)
- [`User.cs`](../src/DryCar/Models/User.cs)
- [`Appointment.cs`](../src/DryCar/Models/Appointment.cs)
- [`Service.cs`](../src/DryCar/Models/Service.cs)
- [`Notification.cs`](../src/DryCar/Models/Notification.cs)

Kullanıcı modeli yalnız profil bilgisini değil, hediye yıkama döngüsünün durumunu da taşır. Randevu modeli onay, arşiv ve hediye kullanımını ayrı alanlarla izler.

## 3. Randevu kuralları

[`AppointmentController.cs`](../src/DryCar/Controllers/AppointmentController.cs) içinde şu akışlar birlikte bulunur:

- uygun slot üretimi,
- geçmiş saat kontrolü,
- toplam kapasite sınırı,
- aynı hizmetin aynı saatte tekrarlanmasını engelleme,
- müşterinin yalnız kendi randevusunu değiştirebilmesi,
- iptalde ayrılmış hediye hakkının geri verilmesi.

Yönetici tarafındaki karşılığı [`AdminController.cs`](../src/DryCar/Controllers/AdminController.cs) içindedir. Bu dosyada hizmet yönetimi, toplu onay ve hediye döngüsü de görülebilir.

## 4. Parola ve yüz doğrulama

Ana HTTP akışı [`AccountController.cs`](../src/DryCar/Controllers/AccountController.cs) içindedir. Parola doğrulandıktan sonra kullanıcıya doğrudan yetki verilmez; yüz doğrulama bitene kadar bekleyen oturum kullanılır.

Yüz işleme algoritmasının tamamı [`extract_vector.py`](../src/DryCar/python/extract_vector.py) dosyasındadır. Bu dosya:

- yüz bulur,
- en büyük yüzü seçer,
- parlaklık ve netlik kontrolü yapar,
- göz işaret noktalarından EAR hesaplar,
- göz kırpma geçişini denetler,
- 128 boyutlu yüz şablonu üretir.

Şablonun veritabanında korunması [`FaceVectorProtector.cs`](../src/DryCar/Services/FaceVectorProtector.cs) ile yapılır. Tarayıcı tarafındaki kare yakalama ve antiforgery başlığı [`face-verification.js`](../src/DryCar/wwwroot/js/face-verification.js) içindedir.

## 5. E-posta ve arka plan işleri

- [`GmailApiEmailSender.cs`](../src/DryCar/Services/GmailApiEmailSender.cs): OAuth token yenileme ve Gmail gönderimi
- [`FreeDealNotifier.cs`](../src/DryCar/Services/FreeDealNotifier.cs): ilk bildirim ve hatırlatma içeriği
- [`FreeDealReminderWorker.cs`](../src/DryCar/Services/FreeDealReminderWorker.cs): periyodik süre kontrolü
- [`AdminSeedWorker.cs`](../src/DryCar/Services/AdminSeedWorker.cs): güvenli ilk yönetici oluşturma

## 6. Harici veriler

- [`KirsehirWeatherService.cs`](../src/DryCar/Services/KirsehirWeatherService.cs)
- [`KirsehirLiveNewsService.cs`](../src/DryCar/Services/KirsehirLiveNewsService.cs)
- [`KirsehirGoogleNewsRssService.cs`](../src/DryCar/Services/KirsehirGoogleNewsRssService.cs)
- [`KirsehirHaberTurkNewsService.cs`](../src/DryCar/Services/KirsehirHaberTurkNewsService.cs)

Harici servis hataları ana sayfayı düşürmez. Servisler hata halinde boş veya `null` sonuç döndürür; arayüz kendi boş durumunu gösterir.

## 7. Arayüz

Razor sayfaları [`Views`](../src/DryCar/Views) altında, genel stiller ve JavaScript dosyaları [`wwwroot`](../src/DryCar/wwwroot) altındadır. Müşteri ve yönetici ekranları aynı model ve denetleyici kurallarını kullanır; kritik doğrulamalar yalnız tarayıcıya bırakılmaz.
