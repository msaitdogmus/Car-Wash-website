using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DryCar.Models;

namespace DryCar.Services;

public interface INewsService
{
    Task<List<NewsItem>> GetLatestAsync(int count = 8, CancellationToken ct = default(CancellationToken));
}
