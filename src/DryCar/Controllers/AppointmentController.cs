using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DryCar.Data;
using DryCar.Models;
using DryCar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DryCar.Controllers;

public class AppointmentController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly IEmailSender _emailSender;

    public AppointmentController(ApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Book()
    {
        ArchiveOldAppointments();
        base.ViewBag.Services = _context.Services.ToList();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Book(int serviceId, DateTime appointmentDate, TimeSpan appointmentTime)
    {
        ArchiveOldAppointments();
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }
        DateTime dt = appointmentDate.Date + appointmentTime;
        if (dt < DateTime.Now)
        {
            base.ModelState.AddModelError("", "Geçmiş saatlere randevu alamazsınız.");
        }
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        int totalExisting = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.AppointmentDate == dt);
        if (totalExisting >= 2)
        {
            base.ModelState.AddModelError("", "Bu saate zaten 2 işlem planlandı. Lütfen farklı bir saat seçin.");
        }
        int serviceExisting = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.AppointmentDate == dt && a.ServiceId == serviceId);
        if (serviceExisting >= 1)
        {
            base.ModelState.AddModelError("", "Bu hizmet için bu saatte slot dolu. Lütfen farklı bir hizmet veya saat seçin.");
        }
        if (!base.ModelState.IsValid)
        {
            base.ViewBag.Services = _context.Services.ToList();
            return View();
        }
        bool applyFree = false;
        User? user = null;
        Service? svc = null;
        try
        {
            user = await _context.Users.FirstOrDefaultAsync((User u) => u.Id == userId.Value);
            svc = await _context.Services.FirstOrDefaultAsync((Service s) => s.Id == serviceId);
            bool isTargetService = svc != null && svc.Name == "İç ve Dış Yıkama";
            DateTime nowUtc = DateTime.UtcNow;
            if (user != null && isTargetService)
            {
                if (user.FreeDealCycleEndAt.HasValue && user.FreeDealCycleEndAt.Value <= nowUtc)
                {
                    user.FreeDealCycleStartAt = nowUtc;
                    user.FreeDealCycleEndAt = nowUtc.AddDays(30.0);
                    user.PaidWashCountInCycle = 0;
                    user.FreeDealsGrantedInCycle = 0;
                    user.FreeDealBalance = 0;
                    user.HasFreeDeal = false;
                    user.FreeDealReservedAppointmentId = null;
                    user.FreeDealReminderSentAt = null;
                }
                if (user.FreeDealBalance > 0 && user.FreeDealCycleEndAt.HasValue && user.FreeDealCycleEndAt.Value > nowUtc && !user.FreeDealReservedAppointmentId.HasValue)
                {
                    applyFree = true;
                    user.FreeDealBalance--;
                    user.HasFreeDeal = user.FreeDealBalance > 0;
                }
            }
            Appointment appt = new Appointment
            {
                UserId = userId.Value,
                ServiceId = serviceId,
                AppointmentDate = dt,
                CreatedAt = DateTime.UtcNow,
                IsArchived = false,
                IsConfirmed = false,
                IsFreeDealApplied = applyFree
            };
            _context.Appointments.Add(appt);
            await _context.SaveChangesAsync();
            if (applyFree && user != null)
            {
                user.FreeDealReservedAppointmentId = appt.Id;
                await _context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            ModelState.AddModelError("", "Bu saat az önce doldu. Lütfen farklı bir saat seçin.");
            ViewBag.Services = await _context.Services.AsNoTracking().ToListAsync();
            return View();
        }
        base.TempData["SuccessMessage"] = (applyFree ? "Randevunuz alındı! Bu randevu hediye hakkınızla işaretlendi (admin onayı sonrası kesinleşir)." : "Randevunuz başarıyla alındı!");
        try
        {
            if (svc != null && svc.Name == "İç ve Dış Yıkama" && user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                string formatted = dt.ToString("dd.MM.yyyy HH:mm");
                string mailSubject = "DRYCAR Randevu Bilgisi";
                string mailBody = $"<p>Merhaba {WebUtility.HtmlEncode(user.FirstName)},</p><p><b>{WebUtility.HtmlEncode(svc.Name)}</b> hizmeti için <b>{formatted}</b> tarihinde randevunuz başarıyla oluşturuldu. Bu randevu şu an admin onayı bekliyor.</p>" + "<p>Randevu durumunuzu ve hediye haklarınızı uygulamamız üzerinden takip edebilirsiniz.</p>";
                await _emailSender.SendEmailAsync(user.Email, mailSubject, mailBody);
            }
        }
        catch
        {
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult MyAppointments()
    {
        ArchiveOldAppointments();
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }
        List<Appointment> upcoming = (from a in _context.Appointments.Include((Appointment a) => a.Service)
                                      where a.UserId == ((int?)userId).Value && !a.IsArchived
                                      orderby a.AppointmentDate
                                      select a).ToList();
        base.ViewBag.Services = _context.Services.ToList();
        return View(upcoming);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Appointment updated)
    {
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Appointment appt = await _context.Appointments.Include((Appointment a) => a.Service).FirstOrDefaultAsync((Appointment a) => a.Id == id && (int?)a.UserId == userId);
        if (appt == null)
        {
            return NotFound();
        }
        if (updated.AppointmentDate < DateTime.Now)
        {
            base.ModelState.AddModelError("", "Geçmiş tarih ve saatlere randevu alınamaz.");
        }
        if (appt.IsFreeDealApplied && updated.ServiceId != appt.ServiceId)
        {
            base.ModelState.AddModelError("", "Hediye olarak işaretli randevuda hizmet değiştirilemez.");
        }
        DateTime requestedDate = updated.AppointmentDate;
        int requestedServiceId = updated.ServiceId;
        if (await _context.Appointments.AnyAsync((Appointment a) => a.Id != id && !a.IsArchived && a.AppointmentDate == requestedDate && a.ServiceId == requestedServiceId))
        {
            base.ModelState.AddModelError("", "Bu hizmet için bu saatte slot dolu. Lütfen farklı bir saat seçin.");
        }
        if (await _context.Appointments.CountAsync((Appointment a) => a.Id != id && !a.IsArchived && a.AppointmentDate == requestedDate) >= 2)
        {
            base.ModelState.AddModelError("", "Bu saatte zaten 2 işlem planlandı. Lütfen farklı bir saat seçin.");
        }
        if (!base.ModelState.IsValid)
        {
            base.ViewBag.Services = _context.Services.ToList();
            return View(updated);
        }
        appt.AppointmentDate = updated.AppointmentDate;
        appt.ServiceId = updated.ServiceId;
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "Bu saat az önce doldu. Lütfen farklı bir saat seçin.");
            ViewBag.Services = await _context.Services.AsNoTracking().ToListAsync();
            return View(updated);
        }
        return RedirectToAction("MyAppointments");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        Appointment? appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId.Value && !a.IsArchived);
        if (appointment is null)
        {
            return NotFound();
        }

        ViewBag.Services = await _context.Services.ToListAsync();
        return View(appointment);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return Unauthorized();
        }
        Appointment appt = await _context.Appointments.FirstOrDefaultAsync((Appointment a) => a.Id == id && (int?)a.UserId == userId);
        if (appt == null)
        {
            return NotFound();
        }
        User user = await _context.Users.FirstOrDefaultAsync((User u) => u.Id == ((int?)userId).Value);
        if (user != null && user.FreeDealReservedAppointmentId == appt.Id)
        {
            user.FreeDealReservedAppointmentId = null;
            if (appt.IsFreeDealApplied)
            {
                user.FreeDealBalance = Math.Min(3, user.FreeDealBalance + 1);
                user.HasFreeDeal = user.FreeDealBalance > 0;
            }
        }
        _context.Appointments.Remove(appt);
        await _context.SaveChangesAsync();
        return RedirectToAction("MyAppointments");
    }

    [HttpGet]
    public JsonResult GetAvailableSlots(DateTime date, int serviceId)
    {
        ArchiveOldAppointments();
        TimeSpan[] slots = GenerateTimeSlots();
        Dictionary<TimeSpan, int> totalCounts = (from a in _context.Appointments
                                                 where !a.IsArchived && a.AppointmentDate.Date == ((DateTime)date).Date
                                                 group a by a.AppointmentDate.TimeOfDay).ToDictionary((IGrouping<TimeSpan, Appointment> g) => g.Key, (IGrouping<TimeSpan, Appointment> g) => g.Count());
        Dictionary<TimeSpan, int> serviceCounts = (from a in _context.Appointments
                                                   where !a.IsArchived && a.AppointmentDate.Date == ((DateTime)date).Date && a.ServiceId == serviceId
                                                   group a by a.AppointmentDate.TimeOfDay).ToDictionary((IGrouping<TimeSpan, Appointment> g) => g.Key, (IGrouping<TimeSpan, Appointment> g) => g.Count());
        List<string> available = (from ts in slots
                                  where (!totalCounts.ContainsKey(ts) || totalCounts[ts] < 2) && (!serviceCounts.ContainsKey(ts) || serviceCounts[ts] < 1)
                                  select ts.ToString("hh\\:mm")).ToList();
        return Json(available);
    }

    private TimeSpan[] GenerateTimeSlots()
    {
        int start = 420;
        int end = 1170;
        int count = (end - start) / 30 + 1;
        return (from i in Enumerable.Range(0, count)
                select TimeSpan.FromMinutes(start + i * 30)).ToArray();
    }

    private void ArchiveOldAppointments()
    {
        DateTime now = DateTime.Now;
        List<Appointment> old = _context.Appointments.Where((Appointment a) => !a.IsArchived && a.AppointmentDate <= now).ToList();
        if (old.Any())
        {
            old.ForEach(delegate (Appointment a)
            {
                a.IsArchived = true;
            });
            _context.SaveChanges();
        }
    }

    [HttpGet]
    public IActionResult PastAppointments()
    {
        ArchiveOldAppointments();
        int? userId = base.HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }
        List<Appointment> past = (from a in _context.Appointments.Include((Appointment a) => a.Service)
                                  where a.UserId == ((int?)userId).Value && a.IsArchived
                                  orderby a.AppointmentDate descending
                                  select a).ToList();
        return View(past);
    }
}
