using System.Linq;
using DryCar.Data;
using DryCar.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DryCar.Controllers;

public class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult MarkLatestRead()
    {
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        Notification? notif = (from n in _context.Notifications
                               where (int?)n.UserId == userId && !n.IsRead
                               orderby n.CreatedAt descending
                               select n).FirstOrDefault();
        if (notif == null)
        {
            return Ok();
        }
        notif.IsRead = true;
        _context.SaveChanges();
        return Ok();
    }
}
