# Katkı rehberi

DryCar Care'e hata düzeltmesi, belge iyileştirmesi ve yeni özellik katkıları memnuniyetle kabul edilir.

## Başlamadan önce

- Büyük değişikliklerde önce kısa bir issue açın.
- Güvenlik açığını herkese açık issue yerine [özel güvenlik bildirimiyle](SECURITY.md) iletin.
- Gerçek müşteri verisi, token, parola, bağlantı cümlesi veya biyometrik örnek commit etmeyin.

## Geliştirme akışı

```bash
git clone https://github.com/msaitdogmus/Car-Wash-website.git
cd Car-Wash-website
dotnet tool restore
dotnet restore DryCar.sln
dotnet build DryCar.sln --configuration Release
```

Python değişiklikleri için:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r src/DryCar/python/requirements.txt
python -m py_compile src/DryCar/python/extract_vector.py
```

## Pull request beklentileri

- Tek bir amaca odaklanan, anlaşılır commitler
- Değişikliğin nedenini anlatan açıklama
- UI değişikliğinde masaüstü ve mobil ekran görüntüsü
- İş kuralı değişikliğinde test veya açık doğrulama adımı
- Yeni yapılandırma anahtarı için `appsettings.example.json` ve README güncellemesi
- Release derlemesinde sıfır uyarı

## Kod tarzı

- Değişken ve sınıf isimleri kod tabanıyla uyumlu İngilizce olabilir.
- Kullanıcıya gösterilen metinler doğal ve anlaşılır Türkçe olmalıdır.
- Yorumlar kodun ne yaptığını tekrar etmek yerine kararın nedenini açıklamalıdır.
- Denetleyicilerde entegrasyon ayrıntısı biriktirmeyin; uygun servise taşıyın.
- Dış girdiyi güvenilir kabul etmeyin ve sunucu tarafında doğrulayın.

Katkı göndererek değişikliğinizin projenin [MIT Lisansı](LICENSE) altında yayımlanmasını kabul etmiş olursunuz.
