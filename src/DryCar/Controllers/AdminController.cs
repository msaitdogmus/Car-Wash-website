using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BCrypt.Net;
using DryCar.Data;
using DryCar.Models;
using DryCar.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DryCar.Controllers;

public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly FreeDealNotifier _notifier;

    private readonly IEmailSender _emailSender;

    public AdminController(ApplicationDbContext context, FreeDealNotifier freeDealNotifier, IEmailSender emailSender)
    {
        _context = context;
        _notifier = freeDealNotifier;
        _emailSender = emailSender;
    }

    private void ArchiveOldAppointments()
    {
        DateTime now = DateTime.Now;
        List<Appointment> oldAppointments = _context.Appointments.Where((Appointment a) => !a.IsArchived && a.AppointmentDate <= now).ToList();
        if (oldAppointments.Any())
        {
            oldAppointments.ForEach(delegate (Appointment a)
            {
                a.IsArchived = true;
            });
            _context.SaveChanges();
        }
    }

    private TimeSpan[] GenerateTimeSlots()
    {
        int startMinutes = 420;
        int endMinutes = 1170;
        int totalSlots = (endMinutes - startMinutes) / 30 + 1;
        return (from i in Enumerable.Range(0, totalSlots)
                select TimeSpan.FromMinutes(startMinutes + i * 30)).ToArray();
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Panel()
    {
        base.HttpContext.Session.Remove("UserId");
        base.HttpContext.Session.Remove("UserName");
        return View("Login");
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public IActionResult Login(string username, string password)
    {
        base.HttpContext.Session.Clear();
        Admin admin = _context.Admins.FirstOrDefault((Admin a) => a.Username == username);
        if (admin != null && BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            base.HttpContext.Session.SetInt32("AdminId", admin.Id);
            return RedirectToAction("Dashboard");
        }
        base.ViewBag.Error = "Kullanıcı adı veya şifre hatalı.";
        return View("Login");
    }

    [HttpGet]
    public IActionResult Dashboard()
    {
        ArchiveOldAppointments();
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        List<Appointment> upcoming = (from a in _context.Appointments.Include((Appointment a) => a.User).Include((Appointment a) => a.Service)
                                      where !a.IsArchived && a.AppointmentDate >= DateTime.Now
                                      orderby a.AppointmentDate
                                      select a).ToList();
        return View(upcoming);
    }

    [HttpGet]
    public IActionResult PastAppointments()
    {
        ArchiveOldAppointments();
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        List<Appointment> past = (from a in _context.Appointments.Include((Appointment a) => a.User).Include((Appointment a) => a.Service)
                                  where a.IsArchived
                                  orderby a.AppointmentDate descending
                                  select a).ToList();
        return View(past);
    }

    [HttpPost]
    public IActionResult DeleteAppointment(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        Appointment appt = _context.Appointments.FirstOrDefault((Appointment a) => a.Id == id && !a.IsArchived && a.AppointmentDate >= DateTime.Now);
        if (appt != null)
        {
            _context.Appointments.Remove(appt);
            _context.SaveChanges();
        }
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public IActionResult Services()
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        List<Service> services = _context.Services.ToList();
        return View(services);
    }

    [HttpGet]
    public IActionResult CreateService()
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        return View(new Service());
    }

    [HttpPost]
    public IActionResult CreateService(Service service)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        if (!base.ModelState.IsValid)
        {
            return View(service);
        }
        _context.Services.Add(service);
        _context.SaveChanges();
        return RedirectToAction("Services");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmArchivedAppointment(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        Appointment appt = await _context.Appointments.Include((Appointment a) => a.Service).Include((Appointment a) => a.User).FirstOrDefaultAsync((Appointment a) => a.Id == id && a.IsArchived);
        if (appt == null)
        {
            base.TempData["ErrorMessage"] = "Randevu bulunamadı.";
            return RedirectToAction("PastAppointments");
        }
        if (appt.IsConfirmed)
        {
            base.TempData["ErrorMessage"] = "Bu randevu zaten onaylanmış.";
            return RedirectToAction("PastAppointments");
        }
        DateTime nowUtc = DateTime.UtcNow;
        appt.IsConfirmed = true;
        appt.ConfirmedAt = nowUtc;
        User user = appt.User;
        if (user == null)
        {
            await _context.SaveChangesAsync();
            base.TempData["SuccessMessage"] = "Randevu onaylandı.";
            return RedirectToAction("PastAppointments");
        }
        if (appt.Service == null || !(appt.Service.Name == "İç ve Dış Yıkama"))
        {
            await _context.SaveChangesAsync();
            base.TempData["SuccessMessage"] = "Randevu onaylandı.";
            return RedirectToAction("PastAppointments");
        }
        EnsureMonthlyFreeDealCycle(user, nowUtc);
        bool cycleExpired = !user.FreeDealCycleEndAt.HasValue || user.FreeDealCycleEndAt.Value <= nowUtc;
        if (appt.IsFreeDealApplied)
        {
            if (!cycleExpired && user.FreeDealBalance > 0)
            {
                user.FreeDealBalance = Math.Max(0, user.FreeDealBalance - 1);
                user.HasFreeDeal = user.FreeDealBalance > 0;
                user.FreeDealRedeemedAt = nowUtc;
                user.FreeDealReservedAppointmentId = null;
                await _context.SaveChangesAsync();
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        string subject = "DRYCAR - Hediye Yıkama Kullanımı";
                        string body = "<p>Merhaba " + WebUtility.HtmlEncode(user.FirstName) + ",</p><p>Bugün onaylanan randevunuz ile İç &amp; Dış yıkama hediye hakkınız kullanılmıştır.</p><p>Yeni hediye haklarınızı takip etmek için uygulamamız üzerinden randevu bilgilerinizi inceleyebilirsiniz.</p>";
                        await _emailSender.SendEmailAsync(user.Email, subject, body);
                    }
                    catch
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Message = "Bedava yıkama kullanımı maili gönderilemedi (Gmail token/yetki hatası).",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }
                base.TempData["SuccessMessage"] = "Bedava yıkama onaylandı ve hak tüketildi.";
                return RedirectToAction("PastAppointments");
            }
            appt.IsFreeDealApplied = false;
            user.FreeDealReservedAppointmentId = null;
            user.PaidWashCountInCycle++;
            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = "Hediye dönemi bittiği (veya bakiye olmadığı) için bu randevu ücretli olarak değerlendirildi.",
                IsRead = false,
                CreatedAt = nowUtc
            });
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    string subject2 = "DRYCAR - Hediye Yıkama Süresi Doldu";
                    string body2 = "<p>Merhaba " + WebUtility.HtmlEncode(user.FirstName) + ",</p><p>İç &amp; Dış yıkama hediye hakkınızın süresi dolduğu veya bakiyeniz olmadığı için bu randevunuz ücretli olarak değerlendirilmiştir.</p><p>Geçerli hediye haklarınızı ve döngü sürenizi uygulamamızdan takip edebilirsiniz.</p>";
                    await _emailSender.SendEmailAsync(user.Email, subject2, body2);
                }
                catch
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Message = "Hediye süresi dolduğunda bilgilendirme maili gönderilemedi (Gmail token/yetki hatası).",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
        }
        else
        {
            user.PaidWashCountInCycle++;
        }
        bool hitEvery3 = user.PaidWashCountInCycle > 0 && user.PaidWashCountInCycle % 3 == 0;
        bool canGrantMore = user.FreeDealsGrantedInCycle < 3;
        if (hitEvery3 && canGrantMore)
        {
            user.FreeDealsGrantedInCycle++;
            user.FreeDealBalance = Math.Min(3, user.FreeDealBalance + 1);
            user.HasFreeDeal = user.FreeDealBalance > 0;
            user.FreeDealGrantedAt = nowUtc;
            user.FreeDealExpiresAt = user.FreeDealCycleEndAt;
            user.FreeDealReminderSentAt = null;
            user.FreeDealNotifiedAt = null;
            await _context.SaveChangesAsync();
            await _notifier.SendInitialAsync(user);
            base.TempData["SuccessMessage"] = $"Randevu onaylandı. Hediye tanımlandı (Döngü içi: {user.FreeDealsGrantedInCycle}/3). Mail gönderildi.";
            return RedirectToAction("PastAppointments");
        }
        user.HasFreeDeal = user.FreeDealBalance > 0;
        await _context.SaveChangesAsync();
        base.TempData["SuccessMessage"] = "Randevu onaylandı.";
        return RedirectToAction("PastAppointments");
    }

    private void EnsureMonthlyFreeDealCycle(User user, DateTime nowUtc)
    {
        if (!user.FreeDealCycleStartAt.HasValue || !user.FreeDealCycleEndAt.HasValue)
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
        else if (user.FreeDealCycleEndAt.Value <= nowUtc)
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
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmAllArchivedAppointments()
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        DateTime nowUtc = DateTime.UtcNow;
        List<Appointment> appts = await (from a in _context.Appointments.Include((Appointment a) => a.Service).Include((Appointment a) => a.User)
                                         where a.IsArchived && !a.IsConfirmed && a.Service != null && a.Service.Name == "İç ve Dış Yıkama"
                                         orderby a.UserId, a.AppointmentDate
                                         select a).ToListAsync();
        if (!appts.Any())
        {
            base.TempData["ErrorMessage"] = "Toplu onaylanacak İç&Dış randevu bulunamadı.";
            return RedirectToAction("PastAppointments");
        }
        int confirmCount = 0;
        int grantedCount = 0;
        int redeemedCount = 0;
        int expiredToPaidCount = 0;
        IEnumerable<IGrouping<int, Appointment>> byUser = from a in appts
                                                          group a by a.UserId;
        foreach (IGrouping<int, Appointment> grp in byUser)
        {
            User user = grp.First().User;
            if (user == null)
            {
                continue;
            }
            EnsureMonthlyFreeDealCycle(user, nowUtc);
            foreach (Appointment appt in grp)
            {
                appt.IsConfirmed = true;
                appt.ConfirmedAt = nowUtc;
                confirmCount++;
                EnsureMonthlyFreeDealCycle(user, nowUtc);
                if (appt.IsFreeDealApplied)
                {
                    if (user.FreeDealCycleEndAt.HasValue && !(user.FreeDealCycleEndAt.Value <= nowUtc) && user.FreeDealBalance > 0)
                    {
                        user.FreeDealBalance = Math.Max(0, user.FreeDealBalance - 1);
                        user.HasFreeDeal = user.FreeDealBalance > 0;
                        user.FreeDealRedeemedAt = nowUtc;
                        user.FreeDealReservedAppointmentId = null;
                        redeemedCount++;
                    }
                    else
                    {
                        appt.IsFreeDealApplied = false;
                        user.FreeDealReservedAppointmentId = null;
                        user.PaidWashCountInCycle++;
                        expiredToPaidCount++;
                    }
                }
                else
                {
                    user.PaidWashCountInCycle++;
                }
                bool hitEvery3 = user.PaidWashCountInCycle > 0 && user.PaidWashCountInCycle % 3 == 0;
                bool canGrantMore = user.FreeDealsGrantedInCycle < 3;
                if (hitEvery3 && canGrantMore)
                {
                    user.FreeDealsGrantedInCycle++;
                    user.FreeDealBalance = Math.Min(3, user.FreeDealBalance + 1);
                    user.HasFreeDeal = user.FreeDealBalance > 0;
                    user.FreeDealGrantedAt = nowUtc;
                    user.FreeDealExpiresAt = user.FreeDealCycleEndAt;
                    user.FreeDealReminderSentAt = null;
                    user.FreeDealNotifiedAt = null;
                    await _context.SaveChangesAsync();
                    grantedCount++;
                    await _notifier.SendInitialAsync(user);
                }
            }
        }
        await _context.SaveChangesAsync();
        base.TempData["SuccessMessage"] = $"{confirmCount} adet randevu onaylandı. {grantedCount} adet hediye tanımlandı (mail+bildirim). {redeemedCount} adet hediye kullanım onayı işlendi. {expiredToPaidCount} adet hediye süresi bittiği için ücretliye çevrildi.";
        return RedirectToAction("PastAppointments");
    }

    [HttpPost]
    public IActionResult DeleteArchivedAppointment(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        Appointment appt = _context.Appointments.Include((Appointment a) => a.Service).FirstOrDefault((Appointment a) => a.Id == id && a.IsArchived);
        if (appt == null)
        {
            return NotFound();
        }
        if (appt.Service.Name.Equals("İç ve Dış Yıkama", StringComparison.InvariantCultureIgnoreCase))
        {
            base.TempData["ErrorMessage"] = "'İç ve Dış Yıkama' servisine ait geçmiş randevu silinemez.";
        }
        else
        {
            _context.Appointments.Remove(appt);
            _context.SaveChanges();
            base.TempData["SuccessMessage"] = "Geçmiş randevu silindi.";
        }
        return RedirectToAction("PastAppointments");
    }

    [HttpPost]
    public IActionResult DeleteAllPastAppointments()
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        List<Appointment> archived = (from a in _context.Appointments.Include((Appointment a) => a.Service)
                                      where a.IsArchived
                                      select a).ToList();
        List<Appointment> toDelete = archived.Where((Appointment a) => !a.Service.Name.Equals("İç ve Dış Yıkama", StringComparison.InvariantCultureIgnoreCase)).ToList();
        int protectedCount = archived.Count - toDelete.Count;
        if (toDelete.Any())
        {
            _context.Appointments.RemoveRange(toDelete);
            _context.SaveChanges();
            base.TempData["SuccessMessage"] = $"{toDelete.Count} adet geçmiş randevu silindi.";
        }
        if (protectedCount > 0)
        {
            base.TempData["ErrorMessage"] = $"{protectedCount} adet 'İç ve Dış Yıkama' geçmiş randevu korunarak silinmedi.";
        }
        return RedirectToAction("PastAppointments");
    }

    [HttpGet]
    public IActionResult EditService(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        Service svc = _context.Services.Find(id);
        if (svc == null)
        {
            return NotFound();
        }
        return View(svc);
    }

    [HttpPost]
    public IActionResult EditService(Service svc)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        if (!base.ModelState.IsValid)
        {
            return View(svc);
        }
        Service existing = _context.Services.Find(svc.Id);
        if (existing == null)
        {
            return NotFound();
        }
        existing.Name = svc.Name;
        existing.Description = svc.Description;
        existing.Price = svc.Price;
        _context.SaveChanges();
        return RedirectToAction("Services");
    }

    [HttpPost]
    public IActionResult DeleteService(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        Service service = _context.Services.Find(id);
        if (service == null)
        {
            return NotFound();
        }
        if (service.Name.Equals("İç ve Dış Yıkama", StringComparison.InvariantCultureIgnoreCase))
        {
            base.TempData["ErrorMessage"] = "Bu hizmet (İç ve Dış Yıkama) silinemez.";
            return RedirectToAction("Services");
        }
        if (_context.Appointments.Any((Appointment a) => a.ServiceId == id && !a.IsArchived))
        {
            base.TempData["ErrorMessage"] = "Bu hizmete bağlı aktif randevular var, silinemez.";
            return RedirectToAction("Services");
        }
        try
        {
            _context.Services.Remove(service);
            _context.SaveChanges();
            base.TempData["SuccessMessage"] = "Hizmet başarıyla silindi.";
        }
        catch (Exception ex)
        {
            base.TempData["ErrorMessage"] = "Silme işleminde hata oluştu: " + ex.Message;
        }
        return RedirectToAction("Services");
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
        DateTime now = DateTime.Now;
        List<string> availableSlots = (from ts in slots.Where(delegate (TimeSpan ts)
            {
                DateTime dateTime = date.Date + ts;
                if (dateTime < now)
                {
                    return false;
                }
                bool flag = !totalCounts.ContainsKey(ts) || totalCounts[ts] < 2;
                bool flag2 = !serviceCounts.ContainsKey(ts) || serviceCounts[ts] < 1;
                return flag && flag2;
            })
                                       select ts.ToString("hh\\:mm")).ToList();
        return Json(availableSlots);
    }

    [HttpGet]
    public IActionResult CreateAppointment()
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        ArchiveOldAppointments();
        base.ViewBag.Users = _context.Users.ToList();
        base.ViewBag.Services = _context.Services.ToList();
        List<string> allSlots = (from ts in GenerateTimeSlots()
                                 select ts.ToString("hh\\:mm")).ToList();
        base.ViewBag.AllSlots = allSlots;
        Appointment model = new Appointment
        {
            AppointmentDate = DateTime.Today.AddHours(7.0),
            CreatedAt = DateTime.Now,
            IsArchived = false,
            IsConfirmed = false,
            IsFreeDealApplied = false
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult CreateAppointment(int userId, int serviceId, DateTime appointmentDate, TimeSpan appointmentTime)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        ArchiveOldAppointments();
        if (!_context.Users.Any((User u) => u.Id == userId))
        {
            base.ModelState.AddModelError("UserId", "Seçilen kullanıcı mevcut değil.");
        }
        if (!_context.Services.Any((Service s) => s.Id == serviceId))
        {
            base.ModelState.AddModelError("ServiceId", "Seçilen hizmet mevcut değil.");
        }
        DateTime dt = appointmentDate.Date + appointmentTime;
        if (dt < DateTime.Now)
        {
            base.ModelState.AddModelError("", "Geçmiş tarih/saat için randevu oluşturamazsınız.");
        }
        DateTime min = appointmentDate.Date.AddHours(7.0);
        DateTime max = appointmentDate.Date.AddHours(19.0).AddMinutes(30.0);
        if (dt < min || dt > max)
        {
            base.ModelState.AddModelError("", "Randevu saati 07:00 - 19:30 arasında olmalıdır.");
        }
        using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
        int totalAtSameSlot = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.AppointmentDate == dt);
        if (totalAtSameSlot >= 2)
        {
            base.ModelState.AddModelError("", "Bu saatte zaten 2 işlem planlandı. Lütfen başka bir saat seçin.");
        }
        int sameServiceAtSameSlot = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.AppointmentDate == dt && a.ServiceId == serviceId);
        if (sameServiceAtSameSlot >= 1)
        {
            base.ModelState.AddModelError("", "Bu hizmet için bu saatte slot dolu. Lütfen farklı bir hizmet veya saat seçin.");
        }
        if (!base.ModelState.IsValid)
        {
            base.ViewBag.Users = _context.Users.ToList();
            base.ViewBag.Services = _context.Services.ToList();
            base.ViewBag.AllSlots = (from ts in GenerateTimeSlots()
                                     select ts.ToString("hh\\:mm")).ToList();
            Appointment model = new Appointment
            {
                UserId = userId,
                ServiceId = serviceId,
                AppointmentDate = dt
            };
            return View(model);
        }
        Appointment newAppt = new Appointment
        {
            UserId = userId,
            ServiceId = serviceId,
            AppointmentDate = dt,
            CreatedAt = DateTime.Now,
            IsArchived = false,
            IsConfirmed = false,
            IsFreeDealApplied = false
        };
        _context.Appointments.Add(newAppt);
        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateException)
        {
            _context.Entry(newAppt).State = EntityState.Detached;
            base.ModelState.AddModelError("", "Bu saat az önce doldu. Lütfen farklı bir saat seçin.");
            base.ViewBag.Users = _context.Users.AsNoTracking().ToList();
            base.ViewBag.Services = _context.Services.AsNoTracking().ToList();
            base.ViewBag.AllSlots = GenerateTimeSlots().Select(ts => ts.ToString("hh\\:mm")).ToList();
            return View(newAppt);
        }
        transaction.Commit();
        base.TempData["SuccessMessage"] = "Yeni randevu başarıyla oluşturuldu.";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public IActionResult EditAppointment(int id)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        ArchiveOldAppointments();
        Appointment appt = _context.Appointments.Include((Appointment a) => a.User).Include((Appointment a) => a.Service).FirstOrDefault((Appointment a) => a.Id == id && !a.IsArchived);
        if (appt == null)
        {
            return NotFound();
        }
        List<string> allSlots = (from ts in GenerateTimeSlots()
                                 select ts.ToString("hh\\:mm")).ToList();
        List<string> availableSlots = GetAvailableSlotsForAdmin(appt.AppointmentDate.Date, appt.ServiceId, appt.Id);
        base.ViewBag.Users = _context.Users.ToList();
        base.ViewBag.Services = _context.Services.ToList();
        base.ViewBag.AllSlots = allSlots;
        base.ViewBag.AvailableSlots = availableSlots;
        return View(appt);
    }

    private List<string> GetAvailableSlotsForAdmin(DateTime date, int serviceId, int? excludeAppointmentId = null)
    {
        ArchiveOldAppointments();
        TimeSpan[] slots = GenerateTimeSlots();
        DateTime now = DateTime.Now;
        IQueryable<Appointment> query = _context.Appointments.Where((Appointment a) => !a.IsArchived && a.AppointmentDate.Date == ((DateTime)date).Date);
        if (excludeAppointmentId.HasValue)
        {
            query = query.Where((Appointment a) => a.Id != ((int?)excludeAppointmentId).Value);
        }
        Dictionary<TimeSpan, int> totals = (from a in query
                                            group a by a.AppointmentDate.TimeOfDay).ToDictionary((IGrouping<TimeSpan, Appointment> g) => g.Key, (IGrouping<TimeSpan, Appointment> g) => g.Count());
        Dictionary<TimeSpan, int> services = (from a in query
                                              where a.ServiceId == serviceId
                                              group a by a.AppointmentDate.TimeOfDay).ToDictionary((IGrouping<TimeSpan, Appointment> g) => g.Key, (IGrouping<TimeSpan, Appointment> g) => g.Count());
        return (from ts in slots.Where(delegate (TimeSpan ts)
            {
                DateTime dateTime = date.Date + ts;
                if (dateTime < now)
                {
                    return false;
                }
                bool flag = !totals.ContainsKey(ts) || totals[ts] < 2;
                bool flag2 = !services.ContainsKey(ts) || services[ts] < 1;
                return flag && flag2;
            })
                select ts.ToString("hh\\:mm")).ToList();
    }

    [HttpPost]
    public IActionResult EditAppointment(int id, int userId, int serviceId, DateTime appointmentDate, TimeSpan appointmentTime)
    {
        if (!base.HttpContext.Session.GetInt32("AdminId").HasValue)
        {
            return RedirectToAction("Panel");
        }
        using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);
        ArchiveOldAppointments();
        Appointment existing = _context.Appointments.FirstOrDefault((Appointment a) => a.Id == id && !a.IsArchived);
        if (existing == null)
        {
            return NotFound();
        }
        if (!_context.Users.Any((User u) => u.Id == userId))
        {
            base.ModelState.AddModelError("UserId", "Seçilen kullanıcı mevcut değil.");
        }
        if (!_context.Services.Any((Service s) => s.Id == serviceId))
        {
            base.ModelState.AddModelError("ServiceId", "Seçilen hizmet mevcut değil.");
        }
        DateTime dt = appointmentDate.Date + appointmentTime;
        if (dt < DateTime.Now)
        {
            base.ModelState.AddModelError("", "Geçmiş tarih/saat için randevu güncelleyemezsiniz.");
        }
        DateTime min = appointmentDate.Date.AddHours(7.0);
        DateTime max = appointmentDate.Date.AddHours(19.0).AddMinutes(30.0);
        if (dt < min || dt > max)
        {
            base.ModelState.AddModelError("", "Randevu saati 07:00 - 19:30 arasında olmalıdır.");
        }
        int totalAtSameSlot = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.Id != id && a.AppointmentDate == dt);
        if (totalAtSameSlot >= 2)
        {
            base.ModelState.AddModelError("", "Bu saatte zaten 2 işlem planlandı. Lütfen başka bir saat seçin.");
        }
        int sameServiceAtSameSlot = _context.Appointments.Count((Appointment a) => !a.IsArchived && a.Id != id && a.AppointmentDate == dt && a.ServiceId == serviceId);
        if (sameServiceAtSameSlot >= 1)
        {
            base.ModelState.AddModelError("", "Bu hizmet için bu saatte slot dolu. Lütfen farklı bir hizmet veya saat seçin.");
        }
        if (!base.ModelState.IsValid)
        {
            base.ViewBag.Users = _context.Users.ToList();
            base.ViewBag.Services = _context.Services.ToList();
            base.ViewBag.AllSlots = (from ts in GenerateTimeSlots()
                                     select ts.ToString("hh\\:mm")).ToList();
            base.ViewBag.AvailableSlots = GetAvailableSlotsForAdmin(appointmentDate.Date, serviceId, id);
            existing.UserId = userId;
            existing.ServiceId = serviceId;
            existing.AppointmentDate = dt;
            return View(existing);
        }
        existing.UserId = userId;
        existing.ServiceId = serviceId;
        existing.AppointmentDate = dt;
        try
        {
            _context.SaveChanges();
            transaction.Commit();
        }
        catch (DbUpdateException)
        {
            transaction.Rollback();
            base.ModelState.AddModelError("", "Bu saat az önce doldu. Lütfen farklı bir saat seçin.");
            base.ViewBag.Users = _context.Users.AsNoTracking().ToList();
            base.ViewBag.Services = _context.Services.AsNoTracking().ToList();
            base.ViewBag.AllSlots = GenerateTimeSlots().Select(ts => ts.ToString("hh\\:mm")).ToList();
            return View(existing);
        }
        base.TempData["SuccessMessage"] = "Randevu güncellendi.";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        base.HttpContext.Session.Remove("AdminId");
        return RedirectToAction("Panel");
    }

    [HttpGet]
    public IActionResult LogoutToCustomer()
    {
        base.HttpContext.Session.Remove("AdminId");
        base.HttpContext.Session.Remove("UserId");
        base.HttpContext.Session.Remove("UserName");
        return RedirectToAction("Login", "Account");
    }
}
