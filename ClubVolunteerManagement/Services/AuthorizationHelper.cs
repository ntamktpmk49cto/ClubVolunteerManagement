using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClubVolunteerManagement.Services;

public static class AuthorizationHelper
{
    // Kiểm tra quyền quản lý CLB (Admin toàn quyền, Manager chỉ quản lý CLB đã được duyệt của mình)
    public static async Task<bool> CanManageClubAsync(ApplicationDbContext context, ClaimsPrincipal user, int clubId)
    {
        if (user.IsInRole("Admin"))
            return true;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return false;

        return await context.ClubMembers.AnyAsync(m =>
            m.ClubId == clubId &&
            m.UserId == userId &&
            m.Status == ClubMemberStatus.Approved &&
            m.Role == ClubRole.Manager);
    }

    // Kiểm tra quyền quản lý Sự kiện dựa trên CLB tổ chức
    public static async Task<bool> CanManageEventAsync(ApplicationDbContext context, ClaimsPrincipal user, int eventId)
    {
        if (user.IsInRole("Admin"))
            return true;

        var ev = await context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev == null)
            return false;

        return await CanManageClubAsync(context, user, ev.ClubId);
    }
}