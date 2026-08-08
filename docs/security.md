# Güvenlik yaklaşımı ve tehdit modeli

Bir kullanıcı F12’ye bastığında HTML, CSS, JavaScript ve tarayıcıdan giden istekleri görebilir. Bu normaldir; tarayıcıya gönderilen hiçbir şey sır değildir. Bu nedenle güvenliği JavaScript’i gizlemeye değil, bütün kritik kararları sunucuda yeniden doğrulamaya dayandırdım.

## Korunan varlıklar

- Müşteri iletişim ve randevu bilgileri
- Parola hashleri ve parola sıfırlama akışı
- Yüz vektörleri
- Yönetici oturumu ve işletme işlemleri
- SQL, Gmail ve Cloudflare kimlik bilgileri

## Tehditler ve karşılıkları

| Olası saldırı | Uygulanan strateji |
| --- | --- |
| Parola deneme / credential stuffing | BCrypt work factor 12, girişe özel IP tabanlı hız limiti, genel hata mesajı |
| CSRF | Antiforgery tokenı, SameSite oturum çerezi, POST isteklerinde Origin/Referer kontrolü |
| XSS | Razor output encoding, CSP, dış script kaynaklarının sınırlandırılması |
| Clickjacking | `frame-ancestors 'none'` ve `X-Frame-Options: DENY` |
| MIME sniffing | `X-Content-Type-Options: nosniff` |
| Oturum çerezinin JavaScript’ten okunması | `HttpOnly`, `Secure`, `SameSite=Strict`, `__Host-` öneki |
| Büyük gövde ile kaynak tüketimi | Global request body limiti, yüz endpoint’i için kontrollü ayrı sınır |
| Randevu yarış durumu | Sunucu tarafı ikinci kontrol, transaction ve filtreli benzersiz indeks |
| SQL injection | EF Core ve parametreli sorgular; kullanıcı girdisi SQL metnine eklenmez |
| Çalınmış veritabanından parola çıkarma | Salt içeren tek yönlü BCrypt hash; düz parola saklanmaz |
| Biyometrik verinin açığa çıkması | Orijinal fotoğrafı saklamama, yüz vektörünü Data Protection ile koruma |
| Fotoğrafla yüz doğrulamasını kandırma | Göz kırpma tabanlı temel canlılık kontrolü, kare/kalite/yüz alanı doğrulaması |
| Gizli anahtarların GitHub’a düşmesi | Secrets dosyalarını repo dışında tutma, public depoda yalnız temsili örnekler |

## Canlı HTTP güvenlik başlıkları

Son production kontrolünde aşağıdaki politikalar aktifti:

- `Content-Security-Policy`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(self), geolocation=(), microphone=()`

Kamera yalnız aynı origin’deki yüz doğrulama akışına bırakılıyor; mikrofon ve konum tarayıcı düzeyinde kapatılıyor.

## Parola sıfırlama

Sıfırlama bağlantısında kullanılan ham token kullanıcıya gönderiliyor, veritabanına ise yalnız SHA-256 özeti yazılıyor. Token kısa süreli ve tek kullanımlı. Veritabanındaki değer tek başına parola değiştirmek için kullanılamıyor.

## Yüz doğrulamanın sınırı

Göz kırpma kontrolü basit fotoğraf tekrarlarına karşı bir engeldir; sertifikalı biyometrik kimlik doğrulama değildir. Gelişmiş replay, maske veya deepfake saldırılarına karşı tek başına yeterli olduğu iddia edilmemektedir. Daha yüksek riskli bir üründe cihaz doğrulama, WebAuthn ve uzman bir liveness sağlayıcısı eklerdim.

## Operasyon güvenliği

- Uygulama yalnız `127.0.0.1` üzerinde dinler.
- İnternet trafiği Cloudflare Tunnel üzerinden gelir.
- Runtime secrets yalnız uygulama kullanıcısının okuyabildiği bir dosyada tutulur.
- Public depoda connection string, OAuth tokenı, admin parolası veya Data Protection anahtarı bulunmaz.
- Geçici test hesapları ve test yüz verileri test sonunda silinir.

