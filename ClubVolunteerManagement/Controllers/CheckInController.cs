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

<<<<<<< Updated upstream
    // 1. TẠO PHIÊN ĐIỂM DANH: Admin toàn hệ thống, Ban chủ nhiệm chỉ được tạo cho sự kiện CLB mình
    [Authorize(Roles = "Admin,ClubManager")]
=======

    // Kiểm tra người dùng có quyền quản lý CLB chứa sự kiện hay không.
    private async Task<bool> CanManageClubAsync(int clubId)
    {
        // Admin được quản lý tất cả CLB.
        if (User.IsInRole("Admin"))
            return true;

        // Lấy ID người dùng đang đăng nhập.
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return false;

        // Chỉ Manager đã được duyệt của đúng CLB mới có quyền.
        return await _context.ClubMembers
            .AsNoTracking()
            .AnyAsync(m =>
                m.ClubId == clubId &&
                m.UserId == userId &&
                m.Status == ClubMemberStatus.Approved &&
                m.Role == ClubRole.Manager);
    }



    // Tạo phiên điểm danh.
    // TODO: Giới hạn action này cho đúng vai trò quản lý sự kiện.
    // POST: /CheckIn/CreateSession
    // Admin hoặc Manager của CLB được mở phiên điểm danh.
    //[Authorize(Roles = "Admin")]
>>>>>>> Stashed changes
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(int eventId)
    {
<<<<<<< Updated upstream
        if (!await AuthorizationHelper.CanManageEventAsync(_context, User, eventId))
            return Forbid();
=======
        // Lấy thông tin sự kiện.
        var eventItem = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == eventId);
>>>>>>> Stashed changes

        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

<<<<<<< Updated upstream
        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.InProgress)
        {
            TempData["Error"] = "Chỉ có thể tạo phiên điểm danh cho sự kiện đã công bố hoặc đang diễn ra.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        // Kiểm tra xem sự kiện đã có phiên nào đang mở chưa
        var existingSession = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.EventId == eventId && s.IsOpen);

        if (existingSession != null)
        {
            return RedirectToAction(nameof(ViewQr), new { eventId = existingSession.EventId });
        }

        var now = DateTime.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
=======
        // Kiểm tra quyền trên CLB tổ chức sự kiện.
        if (!await CanManageClubAsync(eventItem.ClubId))
            return Forbid();

        // Chỉ cho phép mở điểm danh với sự kiện đã được công bố.
        if (eventItem.Status != EventStatus.Published)
        {
            return BadRequest(
                "Chỉ sự kiện đã được công bố mới có thể mở điểm danh.");
        }

        var now = DateTime.UtcNow;

        // Không cho mở điểm danh sau khi sự kiện kết thúc.
        if (eventItem.EndAtUtc <= now)
        {
            return BadRequest(
                "Sự kiện đã kết thúc, không thể mở điểm danh.");
        }

        // Kiểm tra sự kiện đã có phiên điểm danh đang mở chưa.
        var existingSession = await _context.CheckInSessions
            .AnyAsync(s =>
                s.EventId == eventId &&
                s.IsOpen &&
                s.ClosedAtUtc == null &&
                s.TokenExpiresAtUtc > now);

        if (existingSession)
        {
            return BadRequest(
                "Sự kiện đã có phiên điểm danh đang mở.");
        }

        // Tạo token ngẫu nhiên.
        var token = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
>>>>>>> Stashed changes

        // Chỉ lưu mã băm vào database.
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

<<<<<<< Updated upstream
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
=======
        // Tạo URL xác nhận điểm danh.
        var checkInUrl = Url.Action(
            nameof(Attend),
            "CheckIn",
            new
            {
                sessionId = session.Id,
                token
            },
            Request.Scheme);

        // Hiển thị mã QR.
        var html = $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="utf-8">
            <title>Mã QR điểm danh</title>
            <style>
                body {
                    font-family: Arial;
                    text-align: center;
                    padding: 30px;
                }
                img {
                    width: 280px;
                    height: 280px;
                }
            </style>
        </head>
        <body>
            <h2>{{System.Net.WebUtility.HtmlEncode(eventItem.Title)}}</h2>
            <p>Quét mã QR để điểm danh sự kiện.</p>

            <img
                src="/CheckIn/QrImage?sessionId={{session.Id}}&amp;token={{Uri.EscapeDataString(token)}}"
                alt="Mã QR điểm danh" />

            <p>
                Phiên điểm danh hết hạn khi sự kiện kết thúc.
            </p>

            <p>
                <a href="{{System.Net.WebUtility.HtmlEncode(checkInUrl ?? "#")}}">
                    Mở trang xác nhận điểm danh
                </a>
            </p>
        </body>
        </html>
        """;
>>>>>>> Stashed changes

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

        // 1. Kiểm tra tính hợp lệ của phiên QR
        if (!IsSessionValid(session, token))
        {
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");
        }

        var eventId = session!.EventId;

        // 2. Kiểm tra sinh viên có trong danh sách duyệt tham gia không
        var registered = await _context.EventRegistrations
            .AnyAsync(r => r.EventId == eventId &&
                           r.UserId == userId &&
                           r.Status == RegistrationStatus.Confirmed);

        if (!registered)
        {
            return BadRequest("Bạn chưa được duyệt đăng ký tham gia sự kiện này.");
        }

        // 3. Ngăn chặn quét mã nhiều lần (Không được cộng giờ nhiều lần)
        var alreadyCheckedIn = await _context.Attendances
            .AnyAsync(a => a.EventId == eventId && a.UserId == userId);

        if (alreadyCheckedIn)
        {
            return BadRequest("Bạn đã điểm danh sự kiện này rồi, không thể điểm danh lại.");
        }

        // 4. Tạo bản ghi điểm danh
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

        // 5. Chỉ cộng giờ tình nguyện sau khi điểm danh hợp lệ và sự kiện có giờ tình nguyện
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

<<<<<<< Updated upstream
    // 6. ĐÓNG PHIÊN ĐIỂM DANH: Phân quyền Admin & Ban chủ nhiệm CLB
    [Authorize(Roles = "Admin,ClubManager")]
=======
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    // Đóng phiên điểm danh.
    // POST: /CheckIn/CloseSession
    // Admin hoặc Manager của CLB được đóng phiên điểm danh.
    //[Authorize(Roles = "Admin")]
>>>>>>> Stashed changes
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound();

<<<<<<< Updated upstream
        if (!await AuthorizationHelper.CanManageEventAsync(_context, User, session.EventId))
            return Forbid();

=======
        // Kiểm tra quyền quản lý CLB tổ chức sự kiện.
        if (!await CanManageClubAsync(session.Event.ClubId))
            return Forbid();

        if (!session.IsOpen)
        {
            return BadRequest(
                "Phiên điểm danh đã được đóng trước đó.");
        }

>>>>>>> Stashed changes
        session.IsOpen = false;
        session.ClosedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
<<<<<<< Updated upstream
        TempData["Success"] = "Đã đóng phiên điểm danh.";
        return RedirectToAction("Details", "Events", new { id = session.EventId });
=======

        return Content("Đã đóng phiên điểm danh thành công.");
>>>>>>> Stashed changes
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