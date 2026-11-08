using System.Security.Claims;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // 1. CHỈ TẢI CÁC BÀI ĐĂNG CÓ GẮN SỰ KIỆN
        var posts = await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Event)
                .ThenInclude(e => e!.Registrations)
            .Where(p => p.EventId != null) // Lọc bỏ bài đăng thường
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync();

        // 2. Tải danh sách sự kiện được phép đăng (Admin lấy tất cả, Chủ nhiệm lấy sự kiện CLB mình)
        List<Event> availableEvents = new List<Event>();

        if (!string.IsNullOrEmpty(currentUserId))
        {
            if (User.IsInRole("Admin"))
            {
                availableEvents = await _context.Events
                    .Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.InProgress)
                    .OrderByDescending(e => e.StartAtUtc)
                    .ToListAsync();
            }
            else
            {
                var managedClubIds = await _context.ClubMembers
                    .Where(m => m.UserId == currentUserId &&
                                m.Role == ClubRole.Manager &&
                                m.Status == ClubMemberStatus.Approved)
                    .Select(m => m.ClubId)
                    .ToListAsync();

                if (managedClubIds.Any())
                {
                    availableEvents = await _context.Events
                        .Where(e => (e.Status == EventStatus.Published || e.Status == EventStatus.InProgress)
                                 && managedClubIds.Contains(e.ClubId))
                        .OrderByDescending(e => e.StartAtUtc)
                        .ToListAsync();
                }
            }
        }

        ViewBag.AvailableEvents = availableEvents;
        ViewBag.CurrentUserId = currentUserId;

        return View(posts);
    }
}