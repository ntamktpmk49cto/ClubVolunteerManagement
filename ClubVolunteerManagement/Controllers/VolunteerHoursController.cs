
using System.Security.Claims;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers
{
    [Authorize]
    public class VolunteerHoursController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VolunteerHoursController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /VolunteerHours
        public async Task<IActionResult> Index()
        {
            // Lấy ID người dùng đang đăng nhập.
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Challenge();

            // Lấy thông tin sinh viên.
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.FullName,
                    u.StudentCode,
                    u.Email
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            // Lấy lịch sử giờ tình nguyện của chính sinh viên.
            var activities = await _context.VolunteerHours
                .AsNoTracking()
                .Where(h =>
                    h.UserId == userId &&
                    h.Hours > 0 &&
                    h.Attendance.IsValid &&
                    h.Event.Type == EventType.Volunteer)
                .OrderByDescending(h => h.GrantedAtUtc)
                .Select(h => new VolunteerHourItemViewModel
                {
                    EventId = h.EventId,

                    EventTitle = h.Event.Title,

                    ClubName = h.Event.Club.Name,

                    Location = h.Event.Location,

                    EventStartAtUtc = h.Event.StartAtUtc,

                    GrantedAtUtc = h.GrantedAtUtc,

                    Hours = h.Hours
                })
                .ToListAsync();

            // Đếm chứng nhận còn hiệu lực.
            var certificateCount =
                await _context.Certificates
                    .CountAsync(c =>
                        c.UserId == userId &&
                        !c.IsRevoked);

            // Tạo hồ sơ tổng hợp.
            var model = new VolunteerHistoryViewModel
            {
                FullName = user.FullName ?? "Chưa cập nhật",

                StudentCode = user.StudentCode ?? "Chưa cập nhật",

                Email = user.Email ?? string.Empty,

                TotalHours = activities.Sum(x => x.Hours),

                ActivityCount = activities.Count,

                CertificateCount = certificateCount,

                Activities = activities
            };

            return View(model);
        }
    }
}
