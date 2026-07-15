using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DryCar.Models;

namespace DryCar.Services;

public class KirsehirLiveNewsService : INewsService
{
    private readonly KirsehirHaberTurkNewsService _primary;

    private readonly KirsehirGoogleNewsRssService _fallback;

    public KirsehirLiveNewsService(IHttpClientFactory httpClientFactory)
    {
        _primary = new KirsehirHaberTurkNewsService(httpClientFactory);
        _fallback = new KirsehirGoogleNewsRssService(httpClientFactory);
    }

    public async Task<List<NewsItem>> GetLatestAsync(int count = 8, CancellationToken ct = default(CancellationToken))
    {
        List<NewsItem> primaryItems = await _primary.GetLatestAsync(count, ct);
        if (primaryItems != null && primaryItems.Count > 0)
        {
            return primaryItems;
        }
        return (await _fallback.GetLatestAsync(count, ct)) ?? new List<NewsItem>();
    }
}
