
using System.Security.Claims;
using System.Security.Cryptography;

using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
using ClubVolunteerManagement.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers;

[Authorize]
public class CertificatesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CertificatePdfService _pdfService;

    public CertificatesController(
        ApplicationDbContext context,
        CertificatePdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    // GET: /Certificates/Mine
    // Danh sách hoạt động và chứng nhận của sinh viên.
    public async Task<IActionResult> Mine()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var now = DateTime.UtcNow;

        // Lấy giờ tình nguyện và chứng nhận tương ứng.
        var items = await (
            from h in _context.VolunteerHours.AsNoTracking()

            join c in _context.Certificates.AsNoTracking()
                on h.AttendanceId equals c.AttendanceId
                into certificateGroup

            from c in certificateGroup.DefaultIfEmpty()

            where h.UserId == userId
                  && h.Hours > 0
                  && h.Attendance.IsValid
                  && h.Event.Type == EventType.Volunteer
                  && h.Event.Status != EventStatus.Cancelled

            orderby h.GrantedAtUtc descending

            select new CertificateListItemViewModel
            {
                AttendanceId = h.AttendanceId,

                EventTitle = h.Event.Title,

                ClubName = h.Event.Club.Name,

                EventStartAtUtc = h.Event.StartAtUtc,

                EventEndAtUtc = h.Event.EndAtUtc,

                Hours = h.Hours,

                CertificateId =
                    c == null ? (int?)null : c.Id,

                IsRevoked =
                    c != null && c.IsRevoked,

                CanIssue =
                    h.Event.EndAtUtc <= now
            }
        ).ToListAsync();

        return View(items);
    }


    // POST: /Certificates/Issue
    // Sinh viên tự yêu cầu cấp chứng nhận cho lượt tham gia của mình.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int attendanceId)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Challenge();

        var now = DateTime.UtcNow;

        // Chỉ lấy lượt điểm danh của người đang đăng nhập.
        var attendance = await _context.Attendances
            .AsNoTracking()
            .Include(a => a.Event)
            .FirstOrDefaultAsync(a =>
                a.Id == attendanceId &&
                a.UserId == userId);

        if (attendance == null)
            return NotFound();

        // Kiểm tra điểm danh và sự kiện.
        if (!attendance.IsValid ||
            attendance.Event.Type != EventType.Volunteer ||
            attendance.Event.Status == EventStatus.Cancelled ||
            attendance.Event.EndAtUtc > now)
        {
            TempData["Error"] =
                "Hoạt động chưa đủ điều kiện cấp chứng nhận.";

            return RedirectToAction(nameof(Mine));
        }

        // Kiểm tra giờ tình nguyện đã được công nhận.
        var volunteerHour = await _context.VolunteerHours
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.AttendanceId == attendanceId &&
                h.UserId == userId &&
                h.Hours > 0);

        if (volunteerHour == null)
        {
            TempData["Error"] =
                "Không tìm thấy giờ tình nguyện hợp lệ.";

            return RedirectToAction(nameof(Mine));
        }

        // Kiểm tra chứng nhận đã tồn tại chưa.
        var existingCertificate = await _context.Certificates
            .FirstOrDefaultAsync(c =>
                c.AttendanceId == attendanceId);

        if (existingCertificate != null)
        {
            if (existingCertificate.IsRevoked)
            {
                TempData["Error"] =
                    "Chứng nhận này đã bị thu hồi. " +
                    "Vui lòng liên hệ quản trị viên.";

                return RedirectToAction(nameof(Mine));
            }

            // Không cấp trùng, chỉ tải lại bản đã có.
            return RedirectToAction(
                nameof(Download),
                new { id = existingCertificate.Id });
        }

        // Sinh mã xác thực ngẫu nhiên 64 ký tự hex.
        var verificationCode = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));

        var certificate = new Certificate
        {
            AttendanceId = attendance.Id,
            UserId = userId,
            EventId = attendance.EventId,
            VerificationCode = verificationCode,
            IssuedAtUtc = now,
            IsRevoked = false
        };

        _context.Certificates.Add(certificate);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Nếu hai yêu cầu cấp cùng lúc,
            // unique index AttendanceId sẽ ngăn cấp trùng.
            _context.Entry(certificate).State =
                EntityState.Detached;

            var savedCertificate =
                await _context.Certificates
                    .FirstOrDefaultAsync(c =>
                        c.AttendanceId == attendanceId);

            if (savedCertificate != null &&
                !savedCertificate.IsRevoked)
            {
                return RedirectToAction(
                    nameof(Download),
                    new { id = savedCertificate.Id });
            }

            throw;
        }

        TempData["Success"] =
            "Cấp giấy chứng nhận thành công!";

        return RedirectToAction(
            nameof(Download),
            new { id = certificate.Id });
    }


    // GET: /Certificates/Download/1
    // Chỉ chủ sở hữu hoặc Admin được tải PDF.
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUserId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var certificate = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Event)
                .ThenInclude(e => e.Club)
            .Include(c => c.Attendance)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (certificate == null)
            return NotFound();

        if (certificate.UserId != currentUserId &&
            !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (certificate.IsRevoked ||
            !certificate.Attendance.IsValid ||
            certificate.Event.Type != EventType.Volunteer ||
            certificate.Event.Status == EventStatus.Cancelled)
        {
            return BadRequest(
                "Chứng nhận không còn hợp lệ.");
        }

        var volunteerHour = await _context.VolunteerHours
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.AttendanceId == certificate.AttendanceId &&
                h.UserId == certificate.UserId &&
                h.Hours > 0);

        if (volunteerHour == null)
            return BadRequest("Không có giờ tình nguyện hợp lệ.");

        // Tạo URL tuyệt đối để QR truy cập được từ thiết bị khác.
        var verificationUrl = Url.Action(
            nameof(Verify),
            "Certificates",
            new { code = certificate.VerificationCode },
            Request.Scheme,
            Request.Host.Value);

        if (string.IsNullOrWhiteSpace(verificationUrl))
            return BadRequest("Không thể tạo đường dẫn xác thực.");

        // Chuẩn bị dữ liệu PDF.
        var pdfData = new CertificatePdfData
        {
            FullName =
                certificate.User.FullName ?? "Chưa cập nhật",

            StudentCode =
                certificate.User.StudentCode ?? "Chưa cập nhật",

            EventTitle = certificate.Event.Title,

            ClubName = certificate.Event.Club.Name,

            Location = certificate.Event.Location,

            Hours = volunteerHour.Hours,

            EventStartAtLocal =
                certificate.Event.StartAtUtc.AddHours(7),

            EventEndAtLocal =
                certificate.Event.EndAtUtc.AddHours(7),

            IssuedAtLocal =
                certificate.IssuedAtUtc.AddHours(7),

            VerificationCode =
                certificate.VerificationCode,

            VerificationUrl = verificationUrl
        };

        // Tạo file PDF.
        var pdfBytes = _pdfService.Generate(pdfData);

        return File(
            pdfBytes,
            "application/pdf",
            $"ChungNhan_TinhNguyen_{certificate.Id}.pdf");
    }


    // GET: /Certificates/Verify?code=...
    // Cho phép người ngoài tra cứu chứng nhận.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Verify(string? code)
    {
        var model = new CertificateVerifyViewModel
        {
            VerificationCode = code ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(code))
            return View(model);

        var certificate = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Event)
                .ThenInclude(e => e.Club)
            .Include(c => c.Attendance)
            .FirstOrDefaultAsync(c =>
                c.VerificationCode == code);

        if (certificate == null)
            return View(model);

        var volunteerHour = await _context.VolunteerHours
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.AttendanceId == certificate.AttendanceId &&
                h.UserId == certificate.UserId &&
                h.Hours > 0);

        model.Found = true;

        model.IsValid =
            !certificate.IsRevoked &&
            certificate.Attendance.IsValid &&
            certificate.Event.Type == EventType.Volunteer &&
            certificate.Event.Status != EventStatus.Cancelled &&
            volunteerHour != null;

        model.RecipientName =
            certificate.User.FullName ?? "Chưa cập nhật";

        model.EventTitle = certificate.Event.Title;

        model.ClubName = certificate.Event.Club.Name;

        model.Hours = volunteerHour?.Hours ?? 0;

        model.IssuedAtUtc = certificate.IssuedAtUtc;

        return View(model);
    }
}
