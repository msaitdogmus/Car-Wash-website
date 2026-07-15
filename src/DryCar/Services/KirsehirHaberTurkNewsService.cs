using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using DryCar.Models;

namespace DryCar.Services;

public class KirsehirHaberTurkNewsService : INewsService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const string BaseUrl = "https://www.kirsehirhaberturk.com";

    public KirsehirHaberTurkNewsService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<NewsItem>> GetLatestAsync(int count = 8, CancellationToken ct = default(CancellationToken))
    {
        _ = 1;
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("kirsehirhaberturk");
            List<string> urls = new List<string> { "https://www.kirsehirhaberturk.com/", "https://www.kirsehirhaberturk.com/2", "https://www.kirsehirhaberturk.com/3", "https://www.kirsehirhaberturk.com/4", "https://www.kirsehirhaberturk.com/5" };
            List<NewsItem> all = new List<NewsItem>();
            foreach (string url in urls)
            {
                all.AddRange(await ParseHtmlAsync(await client.GetStringAsync(url, ct), ct));
                if (all.Count >= count)
                {
                    break;
                }
            }
            return (from g in all.Where((NewsItem x) => !string.IsNullOrWhiteSpace(x.Title) && !string.IsNullOrWhiteSpace(x.Url)).GroupBy<NewsItem, string>((NewsItem x) => x.Url.Trim(), StringComparer.OrdinalIgnoreCase)
                    select g.First()).Take(count).ToList();
        }
        catch
        {
            return new List<NewsItem>();
        }
    }

    private static async Task<List<NewsItem>> ParseHtmlAsync(string html, CancellationToken ct)
    {
        AngleSharp.IConfiguration config = Configuration.Default;
        IBrowsingContext context = BrowsingContext.New(config);
        var anchors = (from a in (await context.OpenAsync(delegate (VirtualResponse req)
            {
                req.Content(html);
            }, ct)).QuerySelectorAll("a").OfType<IHtmlAnchorElement>()
                       where !string.IsNullOrWhiteSpace(a.Href)
                       select new
                       {
                           Url = NormalizeUrl(a.Href),
                           Title = Clean(a.TextContent),
                           Img = a.QuerySelector("img")?.GetAttribute("src")
                       } into x
                       where x.Url.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                       where x.Url.Contains("kirsehirhaberturk.com", StringComparison.OrdinalIgnoreCase)
                       where !string.IsNullOrWhiteSpace(x.Title)
                       where x.Title.Length >= 6
                       select x).ToList();
        return (from g in Enumerable.GroupBy(anchors, x => x.Url, StringComparer.OrdinalIgnoreCase)
                select g.First() into x
                select new NewsItem
                {
                    Url = x.Url,
                    Title = x.Title,
                    ImageUrl = NormalizeImageUrl(x.Img)
                }).ToList();
        static string Clean(string text)
        {
            text = WebUtility.HtmlDecode(text ?? "");
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text;
        }
        static string? NormalizeImageUrl(string? src)
        {
            if (string.IsNullOrWhiteSpace(src))
            {
                return null;
            }
            src = WebUtility.HtmlDecode(src).Trim();
            if (src.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return src;
            }
            if (src.StartsWith("//"))
            {
                return "https:" + src;
            }
            if (src.StartsWith("/"))
            {
                return "https://www.kirsehirhaberturk.com" + src;
            }
            return "https://www.kirsehirhaberturk.com/" + src.TrimStart('/');
        }
        static string NormalizeUrl(string href)
        {
            href = WebUtility.HtmlDecode(href).Trim();
            if (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }
            if (href.StartsWith("//"))
            {
                return "https:" + href;
            }
            if (href.StartsWith("/"))
            {
                return "https://www.kirsehirhaberturk.com" + href;
            }
            return "https://www.kirsehirhaberturk.com/" + href.TrimStart('/');
        }
    }
}
