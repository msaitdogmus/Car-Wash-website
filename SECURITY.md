# Güvenlik ve gizlilik notu

DryCar Care gerçek kullanıcı ve randevu verileriyle çalışan bir uygulamadır. Bu nedenle herkese açık depo, üretim sisteminin birebir kopyası olarak kullanılmaz.

## Depoda paylaşılmayan bilgiler

- Parolalar ve parola özetleri
- API anahtarları, OAuth istemci sırları ve erişim belirteçleri
- Veritabanı sunucusu ve bağlantı bilgileri
- E-posta hesabı yetkilendirme bilgileri
- Müşteri, randevu ve biyometrik veriler
- Canlı sunucuya özgü dağıtım ayarları

Bu değerler kaynak dosyalarda tutulmaz; üretim ortamında erişimi sınırlandırılmış ortam değişkenlerinden okunur. Örnek yapılandırma hazırlanması gerekirse yalnızca boş yer tutucular kullanılır.

## Uygulama tarafındaki temel önlemler

- Form gönderimlerinde antiforgery doğrulaması
- Müşteri ve yönetici işlemlerinde kimlik doğrulama ve rol kontrolü
- Parolaların BCrypt ile tek yönlü saklanması
- Randevu uygunluğunun istemciye güvenmeden sunucuda doğrulanması
- Harici metinlerin güvenli biçimde ekrana yazılması ve URL protokol kontrolü
- Uygulama servisinin yalnızca yerel arayüzde çalışması

Herkese açık kod örnekleri üretim kodunun sınırlı ve sadeleştirilmiş parçalarıdır. İş kurallarının tamamı güvenlik sağlamak için değil, ticari ve kişisel veri gizliliğini korumak amacıyla özel tutulur; asıl güvenlik kontrolleri her zaman sunucu tarafında uygulanır.
