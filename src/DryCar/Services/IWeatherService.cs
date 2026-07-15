using System.Threading;
using System.Threading.Tasks;
using DryCar.Models;

namespace DryCar.Services;

public interface IWeatherService
{
    Task<WeatherInfo?> GetKirSehirAsync(CancellationToken ct = default(CancellationToken));
}
