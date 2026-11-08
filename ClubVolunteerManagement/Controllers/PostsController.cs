using System.Security.Claims;
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubVolunteerManagement.Controllers;

[Authorize]
public class PostsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PostsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string content, int? eventId, IFormFile? imageFile)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // 1. RÀNG BUỘC: Bắt buộc phải gắn Sự kiện
        if (!eventId.HasValue || eventId.Value <= 0)
        {
            TempData["Error"] = "Bài đăng trang chủ bắt buộc phải chọn Sự kiện.";
            return RedirectToAction("Index", "Home");
        }

        // 2. KIỂM TRA QUYỀN CHỦ NHIỆM / ADMIN
        bool canManage = await AuthorizationHelper.CanManageEventAsync(_context, User, eventId.Value);
        if (!canManage)
        {
            TempData["Error"] = "Bạn không có quyền đăng bài cho sự kiện này.";
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(content) && imageFile == null)
        {
            TempData["Error"] = "Nội dung hoặc hình ảnh bài đăng không được để trống.";
            return RedirectToAction("Index", "Home");
        }

        string? imageUrl = null;
        if (imageFile != null && imageFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "posts");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            imageUrl = $"/uploads/posts/{uniqueFileName}";
        }

        var post = new Post
        {
            UserId = userId,
            EventId = eventId.Value,
            Content = content ?? string.Empty,
            ImageUrl = imageUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã đăng bài thông báo sự kiện thành công!";
        return RedirectToAction("Index", "Home");
    }
}