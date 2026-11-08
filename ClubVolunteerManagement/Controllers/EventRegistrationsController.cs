using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers;

[Authorize]
public class EventRegistrationsController : Controller
{
    private readonly EventRegistrationService _service;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public EventRegistrationsController(
        EventRegistrationService service,
        UserManager<ApplicationUser> users,
        ApplicationDbContext db)
    {
        _service = service;
        _users = users;
        _db = db;
    }

    // POST: /EventRegistrations/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int eventId)
    {
        var userId = _users.GetUserId(User);
        if (userId == null)
            return Challenge();

        // Sử dụng Transaction với IsolationLevel.Serializable để chống đăng ký vượt Capacity cùng thời điểm
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        // Lock bản ghi Event trong CSDL
        var eventItem = await _db.Events
            .FromSqlInterpolated($"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
            .FirstOrDefaultAsync();

        if (eventItem == null)
            return NotFound();

        if (eventItem.Status != EventStatus.Published)
        {
            TempData["Error"] = "Sự kiện hiện tại không mở đăng ký.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        if (eventItem.RegistrationDeadlineUtc <= DateTime.UtcNow)
        {
            TempData["Error"] = "Đã hết hạn đăng ký sự kiện này.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        // Kiểm tra xem sinh viên đã có bản ghi đăng ký trước đó chưa
        var existingReg = await _db.EventRegistrations
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

        if (existingReg != null && existingReg.Status != RegistrationStatus.Cancelled)
        {
            TempData["Error"] = "Bạn đã đăng ký hoặc đang ở trong danh sách chờ của sự kiện này.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        // Đếm số sinh viên đã ở trạng thái chính thức (Confirmed)
        int confirmedCount = await _db.EventRegistrations
            .CountAsync(x => x.EventId == eventId && x.Status == RegistrationStatus.Confirmed);

        // Còn chỗ -> Confirmed | Hết chỗ -> Waitlisted (Đưa vào Danh sách chờ)
        var targetStatus = confirmedCount < eventItem.Capacity
            ? RegistrationStatus.Confirmed
            : RegistrationStatus.Waitlisted;

        if (existingReg != null && existingReg.Status == RegistrationStatus.Cancelled)
        {
            existingReg.Status = targetStatus;
            existingReg.RegisteredAtUtc = DateTime.UtcNow;
        }
        else
        {
            var registration = new EventRegistration
            {
                EventId = eventId,
                UserId = userId,
                Status = targetStatus,
                RegisteredAtUtc = DateTime.UtcNow
            };
            _db.EventRegistrations.Add(registration);
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (targetStatus == RegistrationStatus.Confirmed)
        {
            TempData["Success"] = "Đăng ký tham gia sự kiện thành công!";
        }
        else
        {
            TempData["Warning"] = "Sự kiện đã hết chỗ chính thức! Bạn đã được tự động thêm vào Danh sách chờ.";
        }

        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    // POST: /EventRegistrations/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int eventId)
    {
        var userId = _users.GetUserId(User);
        if (userId == null)
            return Challenge();

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var reg = await _db.EventRegistrations
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

        if (reg == null || reg.Status == RegistrationStatus.Cancelled)
        {
            TempData["Error"] = "Bạn chưa đăng ký sự kiện này.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        bool wasConfirmed = reg.Status == RegistrationStatus.Confirmed;
        reg.Status = RegistrationStatus.Cancelled;
        await _db.SaveChangesAsync();

        // Nếu người hủy thuộc danh sách chính thức, tự động nâng hạng người đăng ký chờ sớm nhất
        if (wasConfirmed)
        {
            var oldestWaitlisted = await _db.EventRegistrations
                .Where(x => x.EventId == eventId && x.Status == RegistrationStatus.Waitlisted)
                .OrderBy(x => x.RegisteredAtUtc)
                .FirstOrDefaultAsync();

            if (oldestWaitlisted != null)
            {
                oldestWaitlisted.Status = RegistrationStatus.Confirmed;
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();

        TempData["Success"] = "Hủy đăng ký thành công!";
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    // GET: /EventRegistrations/Mine
    public async Task<IActionResult> Mine()
    {
        var userId = _users.GetUserId(User);

        var registrations = await _db.EventRegistrations
            .Include(x => x.Event)
                .ThenInclude(x => x.Club)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RegisteredAtUtc)
            .ToListAsync();

        return View(registrations);
    }
}