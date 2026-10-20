using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
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

    // Tạo phiên điểm danh.
    // TODO: Giới hạn action này cho đúng vai trò quản lý sự kiện.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(int eventId)
    {
        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

        var now = DateTime.UtcNow;

        var existingSession = await _context.CheckInSessions
            .AnyAsync(s => s.EventId == eventId && s.IsOpen);

        if (existingSession)
            return BadRequest("Sự kiện đã có phiên điểm danh đang mở.");

        // Token ngẫu nhiên; cơ sở dữ liệu chỉ lưu bản băm.
        var token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        var session = new CheckInSession
        {
            EventId = eventId,
            OpenedAtUtc = now,
            IsOpen = true,
            TokenHash = HashToken(token),
            TokenExpiresAtUtc = eventItem.EndAtUtc
        };

        _context.CheckInSessions.Add(session);
        await _context.SaveChangesAsync();

        var checkInUrl = Url.Action(
            nameof(Attend),
            "CheckIn",
            new { sessionId = session.Id, token },
            Request.Scheme);

        // Hiển thị QR trực tiếp để người quản lý kiểm thử.
        var html = $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="utf-8">
                <title>Mã QR điểm danh</title>
            </head>
            <body style="font-family:Arial;text-align:center;padding:30px">
                <h2>{System.Net.WebUtility.HtmlEncode(eventItem.Title)}</h2>
                <p>Quét mã QR để mở trang điểm danh.</p>
                <img src="/CheckIn/QrImage?sessionId={session.Id}&amp;token={Uri.EscapeDataString(token)}"
                     alt="Mã QR điểm danh" />
                <p>Phiên điểm danh sẽ hết hạn khi sự kiện kết thúc.</p>
                <p><a href="{System.Net.WebUtility.HtmlEncode(checkInUrl ?? "#")}">Mở trang điểm danh</a></p>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    // Tạo ảnh QR chứa URL điểm danh.
    [HttpGet]
    public async Task<IActionResult> QrImage(
        int sessionId,
        string token)
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
        using var data = generator.CreateQrCode(
            url!,
            QRCodeGenerator.ECCLevel.Q);

        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(10);

        return File(png, "image/png");
    }

    // Mở trang xác nhận sau khi quét QR.
/*    [HttpGet]
    public async Task<IActionResult> Attend(
        int sessionId,
        string token)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");

        ViewBag.SessionId = sessionId;
        ViewBag.Token = token;
        ViewBag.EventTitle = session!.Event.Title;

        return View();
    }*/

    // Xác nhận điểm danh và cộng giờ trong cùng transaction.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attend(
        int sessionId,
        string token)
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
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");

        var eventId = session!.EventId;

        var registered = await _context.EventRegistrations
            .AnyAsync(r =>
                r.EventId == eventId &&
                r.UserId == userId &&
                r.Status == RegistrationStatus.Confirmed);

        if (!registered)
            return BadRequest(
                "Bạn chưa được xác nhận đăng ký sự kiện này.");

        var alreadyCheckedIn = await _context.Attendances
            .AnyAsync(a =>
                a.EventId == eventId &&
                a.UserId == userId);

        if (alreadyCheckedIn)
            return BadRequest("Bạn đã điểm danh sự kiện này rồi.");

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

        // Chỉ cộng giờ từ dữ liệu sự kiện trên máy chủ.
        var hours = session.Event.VolunteerHours;

        if (hours < 0)
            return BadRequest("Số giờ tình nguyện không hợp lệ.");

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

        await transaction.CommitAsync();

        return Content(
            $"Điểm danh thành công! Bạn được ghi nhận {hours} giờ tình nguyện.");
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // Đóng phiên điểm danh.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound();

        session.IsOpen = false;
        session.ClosedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Content("Đã đóng phiên điểm danh.");
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool TokenMatches(
        string token,
        string expectedHash)
    {
        try
        {
            var actual = Convert.FromHexString(HashToken(token));
            var expected = Convert.FromHexString(expectedHash);

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
        CheckInSession? session,
        string token)
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