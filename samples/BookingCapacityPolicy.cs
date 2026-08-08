namespace DryCar.PortfolioSamples;

public sealed record BookingRequest(
    int ServiceId,
    DateTime StartsAt,
    int TotalBookingsAtSlot,
    int ServiceBookingsAtSlot);

public sealed record BookingDecision(bool Allowed, string? Reason)
{
    public static BookingDecision Accept() => new(true, null);
    public static BookingDecision Reject(string reason) => new(false, reason);
}

/// <summary>
/// Randevu kapasitesi için sadeleştirilmiş domain politikası.
/// Production akışında bu kontrol, serializable transaction ve veritabanı
/// indeksiyle birlikte yazma anında yeniden uygulanır.
/// </summary>
public sealed class BookingCapacityPolicy
{
    private static readonly TimeOnly OpeningTime = new(7, 30);
    private static readonly TimeOnly LastSlot = new(19, 0);

    public BookingDecision Evaluate(BookingRequest request, DateTime now)
    {
        if (request.ServiceId <= 0)
            return BookingDecision.Reject("Geçerli bir hizmet seçilmelidir.");

        if (request.StartsAt <= now)
            return BookingDecision.Reject("Geçmiş bir saate randevu alınamaz.");

        var time = TimeOnly.FromDateTime(request.StartsAt);
        if (time < OpeningTime || time > LastSlot)
            return BookingDecision.Reject("Seçilen saat çalışma aralığının dışında.");

        if (request.StartsAt.Minute is not (0 or 30))
            return BookingDecision.Reject("Randevular 30 dakikalık aralıklarla açılır.");

        if (request.TotalBookingsAtSlot >= 2)
            return BookingDecision.Reject("Bu saatte toplam araç kapasitesi dolu.");

        if (request.ServiceBookingsAtSlot >= 1)
            return BookingDecision.Reject("Bu hizmet aynı saatte zaten rezerve edilmiş.");

        return BookingDecision.Accept();
    }
}

