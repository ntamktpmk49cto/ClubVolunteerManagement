<<<<<<< HEAD
﻿
using System.Security.Claims;
=======
﻿using System.Security.Claims;
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
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

<<<<<<< HEAD
    // 1. TẠO PHIÊN ĐIỂM DANH
    // Admin quản lý tất cả CLB.
    // Manager chỉ quản lý CLB của mình.
=======
    // Kiểm tra người dùng có quyền quản lý CLB chứa sự kiện hay không
    private async Task<bool> CanManageClubAsync(int clubId)
    {
        // Admin được quản lý tất cả CLB
        if (User.IsInRole("Admin"))
            return true;

        // Lấy ID người dùng đang đăng nhập
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        // Chỉ Manager đã được duyệt của đúng CLB mới có quyền
        return await _context.ClubMembers
            .AsNoTracking()
            .AnyAsync(m =>
                m.ClubId == clubId &&
                m.UserId == userId &&
                m.Status == ClubMemberStatus.Approved &&
                m.Role == ClubRole.Manager);
    }

    // 1. TẠO PHIÊN ĐIỂM DANH (Admin hoặc Manager của CLB)
    [Authorize(Roles = "Admin,ClubManager")]
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(int eventId)
    {
<<<<<<< HEAD
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
=======
        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId);
        if (eventItem == null)
            return NotFound("Không tìm thấy sự kiện.");

        // Kiểm tra quyền quản lý CLB tổ chức sự kiện
        if (!await CanManageClubAsync(eventItem.ClubId))
            return Forbid();

        // Chỉ cho phép mở điểm danh với sự kiện đã được công bố hoặc đang diễn ra
        if (eventItem.Status != EventStatus.Published && eventItem.Status != EventStatus.InProgress)
        {
            TempData["Error"] = "Chỉ có thể tạo phiên điểm danh cho sự kiện đã công bố hoặc đang diễn ra.";
            return RedirectToAction("Details", "Events", new { id = eventId });
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        var now = DateTime.UtcNow;

<<<<<<< HEAD
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
=======
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
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871

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
<<<<<<< HEAD

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
=======
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
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        var checkInUrl = Url.Action(
            nameof(Attend),
            "CheckIn",
<<<<<<< HEAD
            new
            {
                sessionId = session.Id,
                token = session.RawToken
            },
=======
            new { sessionId = session.Id, token = session.RawToken },
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
            Request.Scheme);

        ViewBag.SessionId = session.Id;
        ViewBag.Token = session.RawToken;
        ViewBag.EventTitle = session.Event.Title;
        ViewBag.CheckInUrl = checkInUrl;
<<<<<<< HEAD

        ViewBag.ExpiresAt =
            session.TokenExpiresAtUtc.Value
                .AddHours(7)
                .ToString("HH:mm - dd/MM/yyyy");
=======
        ViewBag.ExpiresAt = session.TokenExpiresAtUtc.Value.AddHours(7).ToString("HH:mm - dd/MM/yyyy");
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871

        return View(session);
    }

<<<<<<< HEAD

    // 3. TẠO HÌNH ẢNH QR
    // Cho phép truy cập công khai nếu có token hợp lệ.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> QrImage(
        int sessionId, string token)
=======
    // 3. TẠO HÌNH ẢNH MÃ QR
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> QrImage(int sessionId, string token)
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
    {
        var session = await _context.CheckInSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

<<<<<<< HEAD
        if (!IsSessionValid(session, token))
        {
            return NotFound(
                "Phiên điểm danh không hợp lệ hoặc đã hết hạn.");
=======
        if (session == null ||
            !session.IsOpen ||
            session.TokenExpiresAtUtc <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(session.TokenHash) ||
            !TokenMatches(token, session.TokenHash))
        {
            return NotFound("Phiên điểm danh không hợp lệ.");
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        var url = Url.Action(
            nameof(Attend),
            "CheckIn",
            new { sessionId, token },
            Request.Scheme);

<<<<<<< HEAD
        if (string.IsNullOrEmpty(url))
            return BadRequest();

        using var generator = new QRCodeGenerator();

        using var data = generator.CreateQrCode(
            url,
            QRCodeGenerator.ECCLevel.Q);

=======
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url!, QRCodeGenerator.ECCLevel.Q);
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        var qr = new PngByteQRCode(data);
        var png = qr.GetGraphic(10);

        return File(png, "image/png");
    }

<<<<<<< HEAD

    // 4. MÀN HÌNH XÁC NHẬN ĐIỂM DANH
    // Sinh viên có thể mở đường dẫn QR.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Attend(
        int sessionId, string token)
=======
    // 4. MÀN HÌNH QUÉT MÃ ĐIỂM DANH
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Attend(int sessionId, string token)
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (!IsSessionValid(session, token))
        {
<<<<<<< HEAD
            return BadRequest(
                "Mã QR không hợp lệ hoặc đã hết hạn.");
=======
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        ViewBag.SessionId = sessionId;
        ViewBag.Token = token;
        ViewBag.EventTitle = session!.Event.Title;

        return View();
    }

<<<<<<< HEAD

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
=======
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
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871

        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

<<<<<<< HEAD
        if (!IsSessionValid(session, token))
        {
            return BadRequest(
                "Mã QR không hợp lệ hoặc đã hết hạn.");
=======
        // 1. Kiểm tra tính hợp lệ của phiên QR
        if (!IsSessionValid(session, token))
        {
            return BadRequest("Mã QR không hợp lệ hoặc đã hết hạn.");
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        var eventId = session!.EventId;

<<<<<<< HEAD
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
=======
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
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        var attendance = new Attendance
        {
            EventId = eventId,
            UserId = userId,
            CheckInSessionId = sessionId,
            CheckedInAtUtc = DateTime.UtcNow,
            IsValid = true
        };

        _context.Attendances.Add(attendance);
<<<<<<< HEAD

        await _context.SaveChangesAsync();

        // Chỉ cộng giờ với sự kiện tình nguyện.
        var hours = session.Event.VolunteerHours;

        if (hours > 0 &&
            session.Event.Type == EventType.Volunteer)
=======
        await _context.SaveChangesAsync();

        // 5. Chỉ cộng giờ tình nguyện sau khi điểm danh hợp lệ và sự kiện có giờ tình nguyện
        var hours = session.Event.VolunteerHours;
        if (hours > 0 && session.Event.Type == EventType.Volunteer)
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
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
<<<<<<< HEAD

=======
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
            await _context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

<<<<<<< HEAD
        TempData["Success"] =
            $"Điểm danh thành công! Bạn được cộng {hours} giờ tình nguyện.";

        return RedirectToAction(
            "Details", "Events", new { id = eventId });
    }


    // 6. ĐÓNG PHIÊN ĐIỂM DANH
    // Admin hoặc Manager của CLB tương ứng.
=======
        TempData["Success"] = $"Điểm danh thành công! Bạn được cộng {hours} giờ tình nguyện.";
        return RedirectToAction("Details", "Events", new { id = eventId });
    }

    // 6. ĐÓNG PHIÊN ĐIỂM DANH (Admin hoặc Ban chủ nhiệm CLB)
    [Authorize(Roles = "Admin,ClubManager")]
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseSession(int sessionId)
    {
        var session = await _context.CheckInSessions
            .Include(s => s.Event)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            return NotFound();

<<<<<<< HEAD
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
=======
        if (!await CanManageClubAsync(session.Event.ClubId))
            return Forbid();

        if (!session.IsOpen)
        {
            TempData["Error"] = "Phiên điểm danh đã được đóng trước đó.";
            return RedirectToAction("Details", "Events", new { id = session.EventId });
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }

        session.IsOpen = false;
        session.ClosedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
<<<<<<< HEAD

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
=======
        TempData["Success"] = "Đã đóng phiên điểm danh thành công.";
        return RedirectToAction("Details", "Events", new { id = session.EventId });
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
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
        }
        catch (FormatException)
        {
            return false;
        }
    }

<<<<<<< HEAD
    private static bool IsSessionValid(
        CheckInSession? session, string token)
=======
    private static bool IsSessionValid(CheckInSession? session, string token)
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
    {
        return session != null &&
               session.IsOpen &&
               session.ClosedAtUtc == null &&
               session.TokenExpiresAtUtc.HasValue &&
               session.TokenExpiresAtUtc.Value > DateTime.UtcNow &&
               !string.IsNullOrWhiteSpace(session.TokenHash) &&
               TokenMatches(token, session.TokenHash);
    }
<<<<<<< HEAD
}
=======
}
>>>>>>> d4131fb9446540709c1d6c213f017e6182342871
