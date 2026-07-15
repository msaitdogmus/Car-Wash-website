# Seçilmiş kod örnekleri

Buradaki parçalar projenin yaklaşımını anlatmak için sadeleştirildi. Canlı sistemdeki sınıf adları, iş kurallarının tamamı ve üretim ayarları özellikle paylaşılmadı. Örneklerde parola, anahtar, bağlantı bilgisi veya kullanıcı verisi bulunmaz.

## Güvenli randevu oluşturma akışı

Randevu isteğini yalnızca arayüzde kontrol etmek yeterli değil. Aynı tarih ve saatin dolu olup olmadığını sunucuda yeniden denetliyor, kullanıcı kimliğini oturumdan alıyor ve hizmet fiyatını istemciden kabul etmek yerine veritabanından okuyoruz.

```csharp
[Authorize]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RandevuOlustur(
    RandevuFormu form,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid)
        return View(form);

    var kullaniciId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(kullaniciId))
        return Challenge();

    // Fiyatı ve hizmet durumunu tarayıcıdan gelen bilgiye göre belirlemiyoruz.
    var hizmet = await _db.Hizmetler
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == form.HizmetId && x.Aktif,
            cancellationToken);

    if (hizmet is null)
    {
        ModelState.AddModelError(nameof(form.HizmetId), "Hizmet bulunamadı.");
        return View(form);
    }

    var saatDolu = await _db.Randevular.AnyAsync(
        x => x.Tarih == form.Tarih
             && x.Saat == form.Saat
             && x.Durum != RandevuDurumu.Iptal,
        cancellationToken);

    if (saatDolu)
    {
        ModelState.AddModelError(nameof(form.Saat), "Bu saat az önce doldu.");
        return View(form);
    }

    _db.Randevular.Add(new Randevu
    {
        KullaniciId = kullaniciId,
        HizmetId = hizmet.Id,
        Tarih = form.Tarih,
        Saat = form.Saat,
        Fiyat = hizmet.Fiyat,
        Durum = RandevuDurumu.Bekliyor
    });

    await _db.SaveChangesAsync(cancellationToken);
    return RedirectToAction(nameof(Randevularim));
}
```

Üretimde bu kontrol, aynı anda gelen iki isteğe karşı veritabanı kısıtı ve kontrollü hata dönüşüyle de desteklenir. Böylece arayüzde boş görünen bir saatin iki kişiye birden verilmesi önlenir.

## Harici içeriği güvenli gösterme

Haber akışından gelen metinleri `innerHTML` ile sayfaya yazmıyoruz. Bağlantının protokolünü kontrol ediyor, metni ise tarayıcının güvenli `textContent` özelliğiyle yerleştiriyoruz.

```javascript
function guvenliBaglanti(deger) {
    try {
        const adres = new URL(deger, window.location.origin);
        const izinli = adres.protocol === "https:" || adres.protocol === "http:";
        return izinli ? adres.href : "#";
    } catch {
        return "#";
    }
}

function haberiGoster(kart, haber) {
    const baslik = kart.querySelector("[data-haber-baslik]");
    const baglanti = kart.querySelector("[data-haber-link]");

    // textContent, dış kaynaktan gelen HTML'in çalışmasını engeller.
    baslik.textContent = String(haber.baslik ?? "Güncel haber");
    baglanti.href = guvenliBaglanti(haber.url);
}
```

Bu örnekler projenin tamamı değildir; okunabilirlik için günlükleme, bildirim, ayrıntılı doğrulama ve bazı iş kuralları çıkarılmıştır.
