# Değişiklik günlüğü

Bu projede yapılan önemli değişiklikler bu dosyada tutulur.

## [1.0.0] - 2026-07-15

### Eklendi

- ASP.NET Core MVC kaynak kodu ve Razor arayüzleri
- Randevu, hizmet, kullanıcı, bildirim ve hediye yıkama akışları
- Python, dlib ve `face_recognition` tabanlı yüz doğrulama
- Göz kırpma temelli temel canlılık kontrolü
- Gmail OAuth 2.0 ile bildirim gönderimi
- Open-Meteo hava durumu ve Kırşehir haber entegrasyonları
- Veri tabanı migration'ları ve örnek yapılandırma
- MIT lisansı, güvenlik belgesi ve GitHub Actions derleme kontrolü

### Güvenlik

- Parolalar BCrypt iş faktörü 12 ile özetleniyor
- Yüz vektörleri ASP.NET Core Data Protection ile korunuyor
- Parola sıfırlama anahtarlarının yalnız SHA-256 özeti saklanıyor
- Güvenli oturum çerezi, otomatik CSRF denetimi ve temel güvenlik başlıkları eklendi
