
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
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

    // 1. TẠO PHIÊN ĐIỂM DANH
    // Admin quản lý tất cả CLB.
    // Manager chỉ quản lý CLB của mình.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(int eventId)
    {
        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

        // Kiểm tra quyền Admin hoặc Manager của đúng CLB.
        if (!await AuthorizationHelper.CanManageEventAsync(
            _context, User, eventId))
        {
            return Forbid();
        }

        if (eventItem.Status != EventStatus.Published &&
            eventItem.Status != EventStatus.InProgress)
        {
            TempData["Error"] =
                "Chỉ sự kiện đã công bố hoặc đang diễn ra mới được mở điểm danh.";

            return RedirectToAction(
                "Details", "Events", new { id = eventId });
        }

        var now = DateTime.UtcNow;

        if (eventItem.EndAtUtc <= now)
        {
            TempData["Error"] = "Sự kiện đã kết thúc.";

            return RedirectToAction(
                "Details", "Events", new { id = eventId });
        }

        // Kiểm tra phiên điểm danh đang mở.
        var existingSession = await _context.CheckInSessions
            .FirstOrDefaultAsync(s =>
                s.EventId == eventId &&
                s.IsOpen &&
                s.ClosedAtUtc == null);

        if (existingSession != null)
        {
            // Nếu phiên hiện tại còn hợp lệ, hiển thị lại QR.
            if (existingSession.TokenExpiresAtUtc.HasValue &&
                existingSession.TokenExpiresAtUtc.Value > now &&
                !string.IsNullOrWhiteSpace(existingSession.RawToken))
            {
                return RedirectToAction(
                    nameof(ViewQr),
                    new { eventId });
            }

            // Phiên cũ hết hạn thì đóng lại.
            existingSession.IsOpen = false;
            existingSession.ClosedAtUtc = now;
        }

        // Sinh token ngẫu nhiên.
        var token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

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

        TempData["Success"] =
            "Đã mở phiên điểm danh thành công!";

        return RedirectToAction(
            nameof(ViewQr),
            new { eventId });
    }


    // 2. XEM LẠI MÃ QR
    // Chỉ Admin hoặc Manager của CLB được xem.
    [HttpGet]
    public async Task<IActionResult> ViewQr(int eventId)
    {
        if (!await AuthorizationHelper.CanManageEventAsync(
            _context, User, eventId))
        {
            return Forbid();
        }

        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s =>
                s.EventId == eventId &&
                s.IsOpen &&
                s.ClosedAtUtc == null);

        if (session == null ||
            !session.TokenExpiresAtUtc.HasValue ||
            session.TokenExpiresAtUtc.Value <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(session.RawToken))
        {
            TempData["Error"] =
                "Sự kiện chưa mở điểm danh hoặc phiên đã hết hạn.";

            return RedirectToAction(
                "Details", "Events", new { id = eventId });
        }

        var checkInUrl = Url.Action(
            nameof(Attend),
            "CheckIn",
            new
            {
                sessionId = session.Id,
                token = session.RawToken
            },
            Request.Scheme);

        ViewBag.SessionId = session.Id;
        ViewBag.Token = session.RawToken;
        ViewBag.EventTitle = session.Event.Title;
        ViewBag.CheckInUrl = checkInUrl;

        ViewBag.ExpiresAt =
            session.TokenExpiresAtUtc.Value
                .AddHours(7)
                .ToString("HH:mm - dd/MM/yyyy");

        return View(session);
    }


    // 3. TẠO HÌNH ẢNH QR
    // Cho phép truy cập công khai nếu có token hợp lệ.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> QrImage(
        int sessionId, string token)
    {
        var session = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
            return NotFound(
                "Phiên điểm danh không hợp lệ hoặc đã hết hạn.");
        }

        var url = Url.Action(
            nameof(Attend),
            "CheckIn",
            new { sessionId, token },
            Request.Scheme);

        if (string.IsNullOrEmpty(url))
            return BadRequest();

        using var generator = new QRCodeGenerator();

        using var data = generator.CreateQrCode(
            url,
            QRCodeGenerator.ECCLevel.Q);

        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(10);

        return File(png, "image/png");
    }


    // 4. MÀN HÌNH XÁC NHẬN ĐIỂM DANH
    // Sinh viên có thể mở đường dẫn QR.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Attend(
        int sessionId, string token)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
            return BadRequest(
                "Mã QR không hợp lệ hoặc đã hết hạn.");
        }

        ViewBag.SessionId = sessionId;
        ViewBag.Token = token;
        ViewBag.EventTitle = session!.Event.Title;

        return View();
    }


    // 5. XÁC NHẬN ĐIỂM DANH VÀ CỘNG GIỜ
    // Người dùng phải đăng nhập.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Attend")]
    public async Task<IActionResult> AttendConfirm(
        int sessionId, string token)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
            return BadRequest(
                "Mã QR không hợp lệ hoặc đã hết hạn.");
        }

        var eventId = session!.EventId;

        // Kiểm tra đã đăng ký và được xác nhận.
        var registered = await _context.EventRegistrations
            .AnyAsync(r =>
                r.EventId == eventId &&
                r.UserId == userId &&
                r.Status == RegistrationStatus.Confirmed);

        if (!registered)
        {
            return BadRequest(
                "Bạn chưa được duyệt đăng ký sự kiện này.");
        }

        // Chặn điểm danh trùng.
        var alreadyCheckedIn =
            await _context.Attendances.AnyAsync(a =>
                a.EventId == eventId &&
                a.UserId == userId);

        if (alreadyCheckedIn)
        {
            return BadRequest(
                "Bạn đã điểm danh sự kiện này rồi.");
        }

        // Tạo dữ liệu điểm danh.
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

        // Chỉ cộng giờ với sự kiện tình nguyện.
        var hours = session.Event.VolunteerHours;

        if (hours > 0 &&
            session.Event.Type == EventType.Volunteer)
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

        TempData["Success"] =
            $"Điểm danh thành công! Bạn được cộng {hours} giờ tình nguyện.";

        return RedirectToAction(
            "Details", "Events", new { id = eventId });
    }


    // 6. ĐÓNG PHIÊN ĐIỂM DANH
    // Admin hoặc Manager của CLB tương ứng.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound();

        if (!await AuthorizationHelper.CanManageEventAsync(
            _context, User, session.EventId))
        {
            return Forbid();
        }

        if (!session.IsOpen ||
            session.ClosedAtUtc != null)
        {
            TempData["Error"] =
                "Phiên điểm danh đã được đóng trước đó.";

            return RedirectToAction(
                "Details", "Events",
                new { id = session.EventId });
        }

        session.IsOpen = false;
        session.ClosedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Đã đóng phiên điểm danh thành công.";

        return RedirectToAction(
            "Details", "Events",
            new { id = session.EventId });
    }


    // 7. HÀM BẢO MẬT TOKEN
    private static string HashToken(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(token)));
    }

    private static bool TokenMatches(
        string token, string expectedHash)
    {
        try
        {
            var actual = Convert.FromHexString(
                HashToken(token));

            var expected = Convert.FromHexString(
                expectedHash);

            return actual.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(
                       actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSessionValid(
        CheckInSession? session, string token)
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
