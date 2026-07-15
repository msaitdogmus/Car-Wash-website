# Güvenlik politikası

DryCar Care müşteri iletişim bilgileri, randevu kayıtları ve biyometrik şablonlarla çalıştığı için güvenlik bildirimlerini ciddiye alır.

## Desteklenen sürüm

| Sürüm | Güvenlik güncellemesi |
| --- | --- |
| `main` / 1.x | Destekleniyor |
| 1.0 öncesi | Desteklenmiyor |

## Açık bildirme

Bir güvenlik sorunu bulursanız herkese açık issue açmayın. GitHub'ın özel güvenlik bildirimini kullanın:

https://github.com/msaitdogmus/Car-Wash-website/security/advisories/new

Bildirimde mümkünse şunları ekleyin:

- Etkilenen dosya veya endpoint
- Sorunu yeniden üretme adımları
- Beklenen ve gerçekleşen davranış
- Olası etki
- Varsa güvenli çözüm önerisi

Gerçek müşteri verisi, erişim tokenı, parola, yüz görüntüsü veya biyometrik şablon göndermeyin. Kanıt için yapay test verisi kullanın.

## Yanıt süreci

- Bildirimin alındığı mümkün olan en kısa sürede doğrulanır.
- Etki ve istismar edilebilirlik incelenir.
- Düzeltme özel dalda hazırlanır ve test edilir.
- Gerekirse token/anahtar döndürme adımları uygulanır.
- Düzeltme yayımlandıktan sonra uygun kapsamda güvenlik notu paylaşılır.

## Kapsam

Aşağıdakiler özellikle önemlidir:

- Kimlik doğrulama veya yönetici yetkisi atlama
- CSRF, XSS, SQL injection ve oturum ele geçirme
- Parola sıfırlama tokenının kötüye kullanılması
- Yüz şablonu veya geçici görüntünün açığa çıkması
- Data Protection anahtar yönetimi hatası
- Gmail OAuth tokenının sızması
- Dosya yolu veya Python süreç argümanı enjeksiyonu
- Randevu kapasitesini veya hediye bakiyesini bozabilen yarış koşulu

Genel mimari ve mevcut savunmalar için [güvenlik ve yüz doğrulama belgesini](docs/GUVENLIK_VE_YUZ_DOGRULAMA.md) okuyun.
