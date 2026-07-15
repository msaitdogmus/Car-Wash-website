using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DryCar.Models;

namespace DryCar.Services;

public class KirsehirGoogleNewsRssService : INewsService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public KirsehirGoogleNewsRssService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<NewsItem>> GetLatestAsync(int count = 8, CancellationToken ct = default(CancellationToken))
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10.0);
            string rssUrl = "https://news.google.com/rss/search?q=K%C4%B1r%C5%9Fehir&hl=tr&gl=TR&ceid=TR:tr";
            XDocument doc = XDocument.Parse(await client.GetStringAsync(rssUrl, ct));
            return (from x in doc.Descendants("item")
                    select new NewsItem
                    {
                        Title = WebUtility.HtmlDecode(x.Element("title")?.Value ?? "").Trim(),
                        Url = (x.Element("link")?.Value ?? "").Trim(),
                        ImageUrl = null
                    } into x
                    where x.Title.Length >= 6 && x.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    select x).Take(count).ToList();
        }
        catch
        {
            return new List<NewsItem>();
        }
    }
}
