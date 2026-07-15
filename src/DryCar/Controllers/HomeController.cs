using System;
using System.Threading;
using System.Threading.Tasks;
using DryCar.Data;
using DryCar.Models;
using DryCar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DryCar.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly INewsService _newsService;

    private readonly IWeatherService _weatherService;

    public HomeController(ApplicationDbContext context, INewsService newsService, IWeatherService weatherService)
    {
        _context = context;
        _newsService = newsService;
        _weatherService = weatherService;
    }

    public IActionResult Index()
    {
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (userId.HasValue)
        {
            User user = _context.Users.Find(userId.Value);
            if (user != null && user.HasFreeDeal && !user.FreeDealNotifiedAt.HasValue)
            {
                base.TempData["NotificationMessage"] = "\ud83c\udf89 Tebrikler! 3 kez İç ve Dış Yıkama yaptınız. 4. Bedava! (7 gün içinde kullanın)";
                user.FreeDealNotifiedAt = DateTime.UtcNow;
                _context.SaveChanges();
            }
        }
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> KirsehirNews([FromQuery] int count = 8, CancellationToken ct = default(CancellationToken))
    {
        return Json(await _newsService.GetLatestAsync(count, ct));
    }

    [HttpGet]
    public async Task<IActionResult> KirsehirWeather(CancellationToken ct = default(CancellationToken))
    {
        return Json(await _weatherService.GetKirSehirAsync(ct));
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Kvkk()
    {
        return View();
    }

    public IActionResult Gizlilik()
    {
        return View();
    }

    public IActionResult Cookies()
    {
        return View();
    }
}
