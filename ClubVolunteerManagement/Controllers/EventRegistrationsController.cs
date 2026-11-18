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
        var user = await _users.GetUserAsync(User);
        if (user == null)
            return Challenge();

        // Bắt buộc sinh viên cập nhật Họ tên & MSSV trước khi đăng ký sự kiện
        if (string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrWhiteSpace(user.StudentCode))
        {
            TempData["Error"] = "Vui lòng cập nhật đầy đủ Họ và tên cùng Mã số sinh viên (MSSV) trước khi đăng ký sự kiện.";
            return Redirect("/Identity/Account/Manage");
        }
        bool isProfileIncomplete =
                string.IsNullOrWhiteSpace(user.FullName) ||
                string.IsNullOrWhiteSpace(user.StudentCode) ||
                string.IsNullOrWhiteSpace(user.SchoolOrFaculty) ||
                string.IsNullOrWhiteSpace(user.Major) ||
                string.IsNullOrWhiteSpace(user.Cohort) ||
                string.IsNullOrWhiteSpace(user.ClassName) ||
                string.IsNullOrWhiteSpace(user.PhoneNumber);
        if (isProfileIncomplete)
        {
            TempData["Error"] = "Vui lòng cập nhật đầy đủ toàn bộ thông tin hồ sơ sinh viên trước khi đăng ký tham gia CLB.";
            return Redirect("/Identity/Account/Manage");
        }

        var userId = user.Id;

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

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

        var existingReg = await _db.EventRegistrations
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

        if (existingReg != null && existingReg.Status != RegistrationStatus.Cancelled)
        {
            TempData["Error"] = "Bạn đã đăng ký hoặc đang ở trong danh sách chờ của sự kiện này.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        int confirmedCount = await _db.EventRegistrations
            .CountAsync(x => x.EventId == eventId && x.Status == RegistrationStatus.Confirmed);

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
        if (userId == null)
            return Challenge();

        var registrations = await _db.EventRegistrations
            .Include(x => x.Event)
                .ThenInclude(x => x.Club)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Event.StartAtUtc)
            .ToListAsync();

        // Tính tổng giờ tình nguyện từ các sự kiện tình nguyện đã xác nhận tham gia và đã hoàn thành
        var completedVolunteerRegs = registrations
            .Where(x => x.Status == RegistrationStatus.Confirmed &&
                        x.Event.Status == EventStatus.Completed &&
                        x.Event.Type == EventType.Volunteer)
            .ToList();

        ViewBag.TotalVolunteerHours = completedVolunteerRegs.Sum(x => x.Event.VolunteerHours);
        ViewBag.CompletedCount = registrations.Count(x =>
            x.Status == RegistrationStatus.Confirmed &&
            x.Event.Status == EventStatus.Completed);
        ViewBag.UpcomingCount = registrations.Count(x =>
            (x.Status == RegistrationStatus.Confirmed || x.Status == RegistrationStatus.Waitlisted) &&
            x.Event.EndAtUtc >= DateTime.UtcNow &&
            x.Event.Status != EventStatus.Cancelled);

        return View(registrations);
    }
}