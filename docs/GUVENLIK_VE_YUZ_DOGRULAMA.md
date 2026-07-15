# Güvenlik ve yüz doğrulama

Bu belge güvenlik özelliklerini pazarlama cümleleriyle değil, kullanılan mekanizma ve sınırlarıyla anlatır. Yüz doğrulama yardımcı bir hesap güvenliği katmanıdır; tek başına resmi kimlik tespiti değildir.

## Korunan varlıklar

- Müşteri hesabı ve oturumu
- Parola özetleri
- Biyometrik yüz şablonları
- Randevu ve iletişim kayıtları
- Gmail OAuth bilgileri
- SQL Server bağlantı bilgisi
- Yönetici paneli

## Parolalar

Parola geri çözülebilir biçimde şifrelenmez. `BCrypt.Net-Next` ile iş faktörü 12 kullanılarak özetlenir. BCrypt her parola için kendi salt değerini üretir ve bu bilgiyi özet metni içinde taşır.

```csharp
string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
bool valid = BCrypt.Net.BCrypt.Verify(candidate, hash);
```

Bu yaklaşım veritabanı sızsa bile parolayı doğrudan açığa çıkarmaz. Yine de zayıf kullanıcı parolaları çevrimdışı tahmin saldırısına karşı risklidir; güçlü parola politikası, hesap tabanlı kilit ve risk analizi ayrıca uygulanmalıdır.

## Parola sıfırlama anahtarı

Token üretimi ve saklama ayrıdır:

1. `RandomNumberGenerator.GetBytes(32)` ile 256 bit rastgele değer üretilir.
2. Ham değer URL güvenli Base64'e çevrilip kullanıcıya gönderilir.
3. Veritabanına yalnız SHA-256 özeti yazılır.
4. Kullanıcının gönderdiği değer tekrar özetlenerek karşılaştırılır.
5. Token 30 dakika sonra veya başarılı kullanımdan hemen sonra geçersiz olur.

Bu sayede veritabanındaki token alanı tek başına parola sıfırlamak için kullanılamaz.

## Biyometrik kayıt sırasında

Tarayıcıdan gelen değer önce veri URI biçimi ve boyut açısından denetlenir. Görüntü geçici işletim sistemi klasörüne rastgele adla yazılır. Bu klasör `wwwroot` altında değildir; dolayısıyla statik dosya sunucusu görüntüyü yayınlayamaz.

Python işlemi aşağıdaki kontrolleri yapar:

- En az bir yüz bulunması
- Birden fazla yüz varsa en büyük alanın seçilmesi
- Yüz alanının en az `80 × 80` piksel olması
- Yüz bölgesinin parlaklığının varsayılan 40–220 aralığında olması
- Laplacian varyansının varsayılan 25 eşiğinin üzerinde olması
- Yüz kodlamasının gerçekten üretilebilmesi

Sonuçta dlib tabanlı 128 kayan noktalı sayıdan oluşan bir şablon çıkar. Ham fotoğraf veritabanına yazılmaz.

## Canlılık kontrolü

Girişte tek kare yerine kısa bir seri alınır. Her karede göz işaret noktaları bulunur ve Eye Aspect Ratio hesaplanır:

```text
EAR = (|p2-p6| + |p3-p5|) / (2 × |p1-p4|)
```

Açık göz eşiği ile kapalı göz eşiğinin aynı seri içinde görülmesi göz kırpma geçişi olarak kabul edilir. Şablon üretiminde açık gözlü en iyi kare seçilir.

Varsayılan eşikler:

| Değer | Varsayılan |
| --- | ---: |
| Açık göz EAR | 0.20 |
| Kapalı göz EAR | 0.18 |
| En küçük yüz alanı | 6.400 px² |
| En düşük netlik | 25 Laplacian varyansı |
| Model | HOG |

Bu değerler kamera, ışık ve kullanıcı profiline göre sahada ölçülerek ayarlanmalıdır.

## Yüz eşleştirme

Kayıtlı ve güncel iki 128 boyutlu şablon arasındaki Öklid mesafesi hesaplanır:

```text
distance = sqrt(Σ(saved[i] - current[i])²)
```

Mesafe `0.6` değerinin altındaysa eşleşme kabul edilir. Daha düşük eşik yanlış kabul oranını azaltırken gerçek kullanıcıyı reddetme ihtimalini artırır. Üretim eşiği hedef kullanıcı kitlesi ve cihazlarla ölçülmelidir.

## Biyometrik şablonun korunması

Şablon veritabanına düz metin olarak yazılmaz. `FaceVectorProtector`, ASP.NET Core Data Protection üzerinde amaca özel bir koruyucu kullanır:

```csharp
provider.CreateProtector("DryCar.FaceVector.v1")
```

Data Protection gizlilik ve bütünlük sağlar. Şablon üzerinde oynanırsa çözme işlemi başarısız olur. Üretimde anahtar halkası:

- web kökü ve uygulama paketinin dışında tutulmalı,
- yalnız servis hesabınca okunabilmeli,
- yedeklenmeli,
- birden fazla instance varsa ortak ve güvenli depoda bulunmalı,
- anahtar kaybına karşı işletim prosedürüne sahip olmalıdır.

Anahtar halkası kaybolursa mevcut yüz şablonları çözülemez; kullanıcıların yeniden kayıt olması gerekir.

## Geçici dosya ve süreç güvenliği

- Kare başına 2 MB sınırı uygulanır.
- Toplam yazılan kare verisi 12 MB ile sınırlanır.
- Kayıt görüntüsü en fazla 5 MB olabilir.
- Dosya adları `Guid` ile üretilir.
- Python işlemi shell açmadan ayrı süreç olarak başlatılır.
- İşlem için 15 saniyelik zaman aşımı vardır.
- Zaman aşımında süreç ağacı sonlandırılır.
- Geçici kareler `finally` içinde silinir.
- Ayrıntılı tanılar varsayılan olarak kapalıdır.

## Oturum ve CSRF

Oturum çerezi aşağıdaki özelliklere sahiptir:

- `HttpOnly`: JavaScript çerezi okuyamaz.
- `Secure`: yalnız HTTPS üzerinden gönderilir.
- `SameSite=Strict`: siteler arası gönderim kısıtlanır.
- `__Host-` öneki: domain kapsamı daralır ve Secure zorunluluğu güçlenir.
- 30 dakikalık boşta kalma süresi

MVC'deki durum değiştiren tüm istekler global `AutoValidateAntiforgeryTokenAttribute` filtresinden geçer. JSON yüz isteği tokenı özel HTTP başlığıyla taşır.

Giriş, kayıt ve parola sıfırlama istekleri IP başına dakikada 10; yüz doğrulama isteği IP başına dakikada 6 denemeyle sınırlandırılır. Ters proxy arkasında gerçek istemci IP'sinin yalnız güvenilen proxy başlıklarından alınması gerekir.

## XSS ve dış kaynaklar

Razor, normal `@value` çıktısını HTML encode eder. JavaScript tarafında kullanıcıya veya dış servise ait metinler ya `textContent` ile yerleştirilir ya da HTML şablonuna girmeden önce açıkça escape edilir. Haber bağlantıları yalnız HTTP/HTTPS protokollerine izin verecek şekilde normalize edilir; dış veri hata verirse sayfanın ana akışı çalışmaya devam eder.

E-posta HTML'i hazırlanırken kullanıcı kaynaklı değerler için ayrıca encode uygulanması önerilir. Yeni entegrasyonlarda URL allowlist ve Content Security Policy birlikte değerlendirilmelidir.

## Gizli ayarlar

Şu değerler repoya yazılmaz:

- SQL bağlantı cümlesi
- Gmail client secret ve refresh token
- İlk yönetici parolası
- Cloudflare tunnel kimlik bilgileri
- Data Protection anahtarları

Yerel geliştirmede .NET user-secrets, üretimde ortam değişkeni veya yönetilen secret kasası kullanılmalıdır. `appsettings.example.json` yalnız anahtar şemasını gösterir.

## Güvenlik başlıkları

Uygulama HTTPS yönlendirmesi ve üretimde HSTS kullanır. Yanıtlara şu temel başlıklar eklenir:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(self), geolocation=(), microphone=()`

Kamera yalnız aynı origin için bırakılır.

## Bilinen sınırlar

Göz kırpma tabanlı canlılık kontrolü:

- gelişmiş video tekrar saldırılarını kesin olarak durdurmaz,
- derin sahte görüntüyü tespit etmez,
- donanım tabanlı derinlik/IR sinyali kullanmaz,
- sertifikalı PAD değerlendirmesi yerine geçmez.

Yüksek riskli bir kullanımda pasif/aktif liveness sağlayıcısı, cihaz bağlama, oran sınırlama, deneme kilidi, risk puanı ve ikinci bağımsız faktör eklenmelidir.

## Operasyon kontrol listesi

- `PythonConfig__DebugMode=false`
- HTTPS dışında erişim yok
- Data Protection anahtar dizini yedekli ve izinleri dar
- Gmail tokenı yalnız gönderim kapsamına sahip
- SQL kullanıcısı en az yetkiyle çalışıyor
- Yedeklerde müşteri ve biyometrik veri şifreli
- Loglarda parola, token, yüz şablonu ve ham görüntü yok
- Bağımlılık ve güvenlik güncellemeleri düzenli uygulanıyor
- Şüpheli girişler ve yönetici işlemleri izleniyor
- İlk yönetici seed parolası ortamdan kaldırılmış
