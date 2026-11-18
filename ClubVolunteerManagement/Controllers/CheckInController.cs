using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
using ClubVolunteerManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace ClubVolunteerManagement.Controllers;

[Authorize]
public class CheckInController : Controller
{
    private readonly ApplicationDbContext _context;

    public CheckInController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Kiểm tra người dùng có quyền quản lý CLB chứa sự kiện hay không
    private async Task<bool> CanManageClubAsync(int clubId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return await _context.ClubMembers
            .AsNoTracking()
            .AnyAsync(m =>
                m.ClubId == clubId &&
                m.UserId == userId &&
                m.Status == ClubMemberStatus.Approved &&
                m.Role == ClubRole.Manager);
    }

    // 1. TẠO PHIÊN ĐIỂM DANH (Admin hoặc Ban chủ nhiệm CLB)
    [Authorize(Roles = "Admin,ClubManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(int eventId)
    {
        // Kiểm tra quyền (Ban chủ nhiệm hoặc Admin)
        if (!await AuthorizationHelper.CanManageEventAsync(_context, User, eventId))
            return Forbid();

        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

        // Chỉ cho phép mở điểm danh với sự kiện đã được công bố hoặc đang diễn ra
        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.InProgress)
        {
            TempData["Error"] = "Chỉ có thể tạo phiên điểm danh cho sự kiện đã công bố hoặc đang diễn ra.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        var now = DateTime.UtcNow;

        // Không cho mở điểm danh sau khi sự kiện kết thúc
        if (eventItem.EndAtUtc <= now)
        {
            TempData["Error"] = "Sự kiện đã kết thúc, không thể mở điểm danh.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        // Kiểm tra xem sự kiện đã có phiên nào đang mở chưa
        var existingSession = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.IsOpen && s.ClosedAtUtc == null && s.TokenExpiresAtUtc > now);

        if (existingSession != null)
        {
            return RedirectToAction(nameof(ViewQr), new { eventId = existingSession.EventId });
        }

        // Tạo token ngẫu nhiên
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var session = new CheckInSession
        {
            EventId = eventId,
            OpenedAtUtc = now,
            IsOpen = true,
            RawToken = token,
            TokenHash = HashToken(token),
            TokenExpiresAtUtc = eventItem.EndAtUtc
        };

        _context.CheckInSessions.Add(session);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(ViewQr), new { eventId = session.EventId });
    }

    // 2. XEM LẠI MÃ QR
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ViewQr(int eventId)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.IsOpen);

        if (session == null ||
            session.ClosedAtUtc != null ||
            !session.TokenExpiresAtUtc.HasValue ||
            session.TokenExpiresAtUtc.Value <= DateTime.UtcNow ||
            string.IsNullOrEmpty(session.RawToken))
        {
            TempData["Error"] = "Sự kiện hiện chưa mở phiên điểm danh hoặc phiên đã hết hạn.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        var checkInUrl = Url.Action(
            nameof(Attend),
            "CheckIn",
            new { sessionId = session.Id, token = session.RawToken },
            Request.Scheme);

        ViewBag.SessionId = session.Id;
        ViewBag.Token = session.RawToken;
        ViewBag.EventTitle = session.Event.Title;
        ViewBag.CheckInUrl = checkInUrl;
        ViewBag.ExpiresAt = session.TokenExpiresAtUtc.Value.AddHours(7).ToString("HH:mm - dd/MM/yyyy");

        return View(session);
    }

    // 3. TẠO HÌNH ẢNH MÃ QR
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> QrImage(int sessionId, string token)
    {
        var session = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null ||
            !session.IsOpen ||
            session.TokenExpiresAtUtc <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(session.TokenHash) ||
            !TokenMatches(token, session.TokenHash))
        {
            return NotFound("Phiên điểm danh không hợp lệ.");
        }

        var url = Url.Action(
            nameof(Attend),
            "CheckIn",
            new { sessionId, token },
            Request.Scheme);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url!, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(10);

        return File(png, "image/png");
    }

    // 4. MÀN HÌNH QUÉT MÃ ĐIỂM DANH
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Attend(int sessionId, string token)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");
        }

        ViewBag.SessionId = sessionId;
        ViewBag.Token = token;
        ViewBag.EventTitle = session!.Event.Title;

        return View();
    }

    // 5. XÁC NHẬN ĐIỂM DANH & CỘNG GIỜ TÌNH NGUYỆN
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Attend")]
    public async Task<IActionResult> AttendConfirm(int sessionId, string token)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");
        }

        var eventId = session!.EventId;

        var registered = await _context.EventRegistrations
            .AnyAsync(r => r.EventId == eventId &&
                           r.UserId == userId &&
                           r.Status == RegistrationStatus.Confirmed);

        if (!registered)
        {
            return BadRequest("Bạn chưa được duyệt đăng ký tham gia sự kiện này.");
        }

        var alreadyCheckedIn = await _context.Attendances
            .AnyAsync(a => a.EventId == eventId && a.UserId == userId);

        if (alreadyCheckedIn)
        {
            return BadRequest("Bạn đã điểm danh sự kiện này rồi, không thể điểm danh lại.");
        }

        var attendance = new Attendance
        {
            EventId = eventId,
            UserId = userId,
            CheckInSessionId = sessionId,
            CheckedInAtUtc = DateTime.UtcNow,
            IsValid = true
        };

        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        var hours = session.Event.VolunteerHours;
        if (hours > 0 && session.Event.Type == EventType.Volunteer)
        {
            var volunteerHour = new VolunteerHour
            {
                AttendanceId = attendance.Id,
                UserId = userId,
                EventId = eventId,
                Hours = hours,
                GrantedAtUtc = DateTime.UtcNow
            };

            _context.VolunteerHours.Add(volunteerHour);
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        TempData["Success"] = $"Điểm danh thành công! Bạn được cộng {hours} giờ tình nguyện.";
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    // 6. ĐÓNG PHIÊN ĐIỂM DANH (Admin hoặc Ban chủ nhiệm CLB)
    [Authorize(Roles = "Admin,ClubManager")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound();

        if (!await CanManageClubAsync(session.Event.ClubId))
            return Forbid();

        if (!session.IsOpen)
        {
            TempData["Error"] = "Phiên điểm danh đã được đóng trước đó.";
            return RedirectToAction("Details", "Events", new { id = session.EventId });
        }

        session.IsOpen = false;
        session.ClosedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã đóng phiên điểm danh thành công.";
        return RedirectToAction("Details", "Events", new { id = session.EventId });
    }

    // 7. XEM DANH SÁCH ĐIỂM DANH
    [HttpGet]
    public async Task<IActionResult> AttendanceList(int eventId)
    {
        // Kiểm tra quyền (chỉ Admin hoặc Ban chủ nhiệm mới được xem)
        if (!await AuthorizationHelper.CanManageEventAsync(_context, User, eventId))
            return Forbid();

        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

        // Lấy danh sách sinh viên ĐÃ ĐƯỢC DUYỆT tham gia sự kiện này
        var registrations = await _context.EventRegistrations
            .Include(r => r.User)
            .Where(r => r.EventId == eventId && r.Status == RegistrationStatus.Confirmed)
            .ToListAsync();

        // Lấy danh sách những người ĐÃ ĐIỂM DANH
        var attendances = await _context.Attendances
            .Where(a => a.EventId == eventId)
            .ToListAsync();

        // Ghép 2 danh sách lại với nhau
        var studentList = registrations.Select(r => new AttendanceStudentViewModel
        {
            StudentCode = r.User.StudentCode,
            FullName = r.User.FullName,
            Email = r.User.Email,
            IsCheckedIn = attendances.Any(a => a.UserId == r.UserId)
        }).OrderBy(s => s.StudentCode).ToList();

        var viewModel = new EventAttendanceViewModel
        {
            EventId = eventId,
            EventTitle = eventItem.Title,
            TotalRegistered = studentList.Count,
            TotalCheckedIn = studentList.Count(s => s.IsCheckedIn),
            Students = studentList
        };

        return View(viewModel);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool TokenMatches(string token, string expectedHash)
    {
        try
        {
            var actual = Convert.FromHexString(HashToken(token));
            var expected = Convert.FromHexString(expectedHash);

            return actual.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSessionValid(CheckInSession? session, string token)
    {
        return session != null &&
               session.IsOpen &&
               session.ClosedAtUtc == null &&
               session.TokenExpiresAtUtc.HasValue &&
               session.TokenExpiresAtUtc.Value > DateTime.UtcNow &&
               !string.IsNullOrWhiteSpace(session.TokenHash) &&
               TokenMatches(token, session.TokenHash);
    }
}