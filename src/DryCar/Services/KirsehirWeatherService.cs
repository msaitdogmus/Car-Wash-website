using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DryCar.Models;

namespace DryCar.Services;

public class KirsehirWeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;

    private const double Lat = 39.1461;

    private const double Lon = 34.1606;

    public KirsehirWeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<WeatherInfo?> GetKirSehirAsync(CancellationToken ct = default(CancellationToken))
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("openmeteo");
            string tz = Uri.EscapeDataString("Europe/Istanbul");
            string url = $"v1/forecast?latitude={39.1461}&longitude={34.1606}&current=temperature_2m,wind_speed_10m,apparent_temperature,weather_code,relative_humidity_2m&timezone={tz}";
            string json = await client.GetStringAsync(url, ct);
            if (string.IsNullOrWhiteSpace(json) || json[0] != '{')
            {
                return null;
            }
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            double? temp = TryGetDouble(current, "temperature_2m");
            if (!temp.HasValue)
            {
                return null;
            }
            double wind = TryGetDouble(current, "wind_speed_10m").GetValueOrDefault();
            double feels = TryGetDouble(current, "apparent_temperature") ?? temp.Value;
            int code = TryGetInt(current, "weather_code").GetValueOrDefault();
            int humidity = TryGetInt(current, "relative_humidity_2m").GetValueOrDefault();
            return new WeatherInfo
            {
                City = "Kırşehir",
                TemperatureC = temp.Value,
                WindKmh = wind,
                FeelsLikeC = feels,
                Humidity = humidity,
                Summary = MapWeatherCode(code)
            };
        }
        catch
        {
            return null;
        }
    }

    private static double? TryGetDouble(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el))
        {
            return null;
        }
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var v))
        {
            return v;
        }
        return null;
    }

    private static int? TryGetInt(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el))
        {
            return null;
        }
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
        {
            return v;
        }
        return null;
    }

    private static string MapWeatherCode(int code)
    {
        switch (code)
        {
            case 0:
                return "Açık";
            case 1:
            case 2:
                return "Az Bulutlu";
            case 3:
                return "Bulutlu";
            case 45:
            case 48:
                return "Sisli";
            case 51:
            case 53:
            case 55:
                return "Çisenti";
            case 61:
            case 63:
            case 65:
                return "Yağmurlu";
            case 71:
            case 73:
            case 75:
                return "Karlı";
            case 80:
            case 81:
            case 82:
                return "Sağanak";
            case 95:
            case 96:
            case 99:
                return "Fırtınalı";
            default:
                return "Parçalı Bulutlu";
        }
    }
}
