
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace ClubVolunteerManagement.Controllers
{
    [Authorize]
    public class ClubMembersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClubMembersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Kiểm tra quyền quản lý một CLB
        private async Task<bool> CanManageClubAsync(int clubId)
        {
            if (User.IsInRole("Admin"))
                return true;

            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return false;

            return await _context.ClubMembers.AnyAsync(m =>
                m.ClubId == clubId &&
                m.UserId == userId &&
                m.Status == ClubMemberStatus.Approved &&
                m.Role == ClubRole.Manager);
        }

        // ===========================
        // 1. SINH VIÊN THAM GIA CLB
        // POST: /ClubMembers/Join
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int clubId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Challenge();

            var clubExists = await _context.Clubs
                .AnyAsync(c => c.Id == clubId);

            if (!clubExists)
                return NotFound();

            var member = await _context.ClubMembers
                .FirstOrDefaultAsync(m =>
                    m.ClubId == clubId &&
                    m.UserId == userId);

            if (member != null)
            {
                if (member.Status == ClubMemberStatus.Pending)
                {
                    TempData["Error"] =
                        "Bạn đã gửi yêu cầu tham gia CLB này.";
                }
                else if (member.Status == ClubMemberStatus.Approved)
                {
                    TempData["Error"] =
                        "Bạn đã là thành viên của CLB này.";
                }
                else
                {
                    // Cho phép gửi lại yêu cầu sau khi bị từ chối.
                    member.Status = ClubMemberStatus.Pending;
                    member.Role = ClubRole.Member;
                    member.JoinedAtUtc = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    TempData["Success"] =
                        "Đã gửi lại yêu cầu tham gia CLB!";
                }

                return RedirectToAction(
                    "Details", "Clubs", new { id = clubId });
            }

            var newMember = new ClubMember
            {
                ClubId = clubId,
                UserId = userId,
                Role = ClubRole.Member,
                Status = ClubMemberStatus.Pending,
                JoinedAtUtc = DateTime.UtcNow
            };

            _context.ClubMembers.Add(newMember);

            try
            {
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Gửi yêu cầu tham gia CLB thành công!";
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sql &&
                (sql.Number == 2601 || sql.Number == 2627))
            {
                TempData["Error"] =
                    "Yêu cầu tham gia đã tồn tại.";
            }

            return RedirectToAction(
                "Details", "Clubs", new { id = clubId });
        }

        // ===========================
        // 2. DANH SÁCH CHỜ DUYỆT
        // GET: /ClubMembers/Pending?clubId=1
        // ===========================
        [HttpGet]
        public async Task<IActionResult> Pending(int clubId)
        {
            var club = await _context.Clubs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clubId);

            if (club == null)
                return NotFound();

            if (!await CanManageClubAsync(clubId))
                return Forbid();

            var members = await _context.ClubMembers
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m =>
                    m.ClubId == clubId &&
                    m.Status == ClubMemberStatus.Pending)
                .OrderBy(m => m.JoinedAtUtc)
                .ToListAsync();

            ViewData["ClubId"] = clubId;
            ViewData["ClubName"] = club.Name;

            return View(members);
        }

        // ===========================
        // 3. DUYỆT THÀNH VIÊN
        // POST: /ClubMembers/Approve
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int memberId)
        {
            var member = await _context.ClubMembers
                .FindAsync(memberId);

            if (member == null)
                return NotFound();

            if (!await CanManageClubAsync(member.ClubId))
                return Forbid();

            if (member.Status != ClubMemberStatus.Pending)
            {
                TempData["Error"] =
                    "Yêu cầu này đã được xử lý.";

                return RedirectToAction(
                    nameof(Pending),
                    new { clubId = member.ClubId });
            }

            member.Status = ClubMemberStatus.Approved;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Duyệt thành viên thành công!";

            return RedirectToAction(
                nameof(Pending),
                new { clubId = member.ClubId });
        }

        // ===========================
        // 4. TỪ CHỐI THÀNH VIÊN
        // POST: /ClubMembers/Reject
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int memberId)
        {
            var member = await _context.ClubMembers
                .FindAsync(memberId);

            if (member == null)
                return NotFound();

            if (!await CanManageClubAsync(member.ClubId))
                return Forbid();

            if (member.Status != ClubMemberStatus.Pending)
            {
                TempData["Error"] =
                    "Yêu cầu này đã được xử lý.";

                return RedirectToAction(
                    nameof(Pending),
                    new { clubId = member.ClubId });
            }

            member.Status = ClubMemberStatus.Rejected;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Đã từ chối yêu cầu tham gia.";

            return RedirectToAction(
                nameof(Pending),
                new { clubId = member.ClubId });
        }

        // ===========================
        // 5. CLB CỦA TÔI
        // GET: /ClubMembers/MyClubs
        // ===========================
        [HttpGet]
        public async Task<IActionResult> MyClubs()
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Challenge();

            var memberships = await _context.ClubMembers
                .Include(m => m.Club)
                .AsNoTracking()
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.JoinedAtUtc)
                .ToListAsync();

            return View(memberships);
        }
    
 // GET: /ClubMembers/Members?clubId=1
[HttpGet]
        public async Task<IActionResult> Members(int clubId)
        {
            var club = await _context.Clubs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clubId);

            if (club == null)
                return NotFound();

            if (!await CanManageClubAsync(clubId))
                return Forbid();

            var members = await _context.ClubMembers
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m =>
                    m.ClubId == clubId &&
                    m.Status == ClubMemberStatus.Approved)
                .OrderBy(m => m.Role == ClubRole.Manager ? 0 : 1)
                .ThenBy(m => m.User.FullName)
                .ToListAsync();

            ViewData["ClubId"] = clubId;
            ViewData["ClubName"] = club.Name;

            return View(members);
        }

        // POST: /ClubMembers/Promote
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promote(int memberId)
        {
            var member = await _context.ClubMembers
                .FindAsync(memberId);

            if (member == null)
                return NotFound();

            if (member.Status != ClubMemberStatus.Approved)
            {
                TempData["Error"] =
                    "Chỉ bổ nhiệm thành viên đã được duyệt.";
            }
            else if (member.Role == ClubRole.Manager)
            {
                TempData["Error"] =
                    "Sinh viên đã là Ban chủ nhiệm.";
            }
            else
            {
                member.Role = ClubRole.Manager;
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Bổ nhiệm Ban chủ nhiệm thành công!";
            }

            return RedirectToAction(
                nameof(Members),
                new { clubId = member.ClubId });
        }

        // POST: /ClubMembers/Demote
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Demote(int memberId)
        {
            var member = await _context.ClubMembers
                .FindAsync(memberId);

            if (member == null)
                return NotFound();

            if (member.Status != ClubMemberStatus.Approved ||
                member.Role != ClubRole.Manager)
            {
                TempData["Error"] =
                    "Người này không phải Ban chủ nhiệm hợp lệ.";
            }
            else
            {
                member.Role = ClubRole.Member;
                await _context.SaveChangesAsync();

                TempData["Success"] =
                    "Đã chuyển về thành viên thường.";
            }

            return RedirectToAction(
                nameof(Members),
                new { clubId = member.ClubId });
        }

        // POST: /ClubMembers/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int memberId)
        {
            var member = await _context.ClubMembers
                .FindAsync(memberId);

            if (member == null)
                return NotFound();

            if (!await CanManageClubAsync(member.ClubId))
                return Forbid();

            var currentUserId = _userManager.GetUserId(User);
            bool isAdmin = User.IsInRole("Admin");

            if (member.UserId == currentUserId)
            {
                TempData["Error"] =
                    "Không thể tự xóa mình bằng chức năng quản lý.";

                return RedirectToAction(
                    nameof(Members),
                    new { clubId = member.ClubId });
            }

            if (member.Role == ClubRole.Manager && !isAdmin)
                return Forbid();

            if (member.Status != ClubMemberStatus.Approved)
            {
                TempData["Error"] =
                    "Chỉ xóa thành viên đã được duyệt.";

                return RedirectToAction(
                    nameof(Members),
                    new { clubId = member.ClubId });
            }

            int clubId = member.ClubId;

            _context.ClubMembers.Remove(member);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Đã xóa thành viên khỏi CLB.";

            return RedirectToAction(
                nameof(Members),
                new { clubId });
        }

        // POST: /ClubMembers/Leave
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int clubId)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == null)
                return Challenge();

            var member = await _context.ClubMembers
                .FirstOrDefaultAsync(m =>
                    m.ClubId == clubId &&
                    m.UserId == userId);

            if (member == null)
                return NotFound();

            if (member.Role == ClubRole.Manager &&
                member.Status == ClubMemberStatus.Approved)
            {
                TempData["Error"] =
                    "Ban chủ nhiệm cần được thu hồi quyền trước khi rời CLB.";

                return RedirectToAction(
                    "Details", "Clubs", new { id = clubId });
            }

            if (member.Status != ClubMemberStatus.Approved &&
                member.Status != ClubMemberStatus.Pending)
            {
                return BadRequest("Trạng thái không hợp lệ.");
            }

            bool wasPending =
                member.Status == ClubMemberStatus.Pending;

            _context.ClubMembers.Remove(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = wasPending
                ? "Đã hủy yêu cầu tham gia CLB."
                : "Bạn đã rời câu lạc bộ thành công.";

            return RedirectToAction(nameof(MyClubs));
        }


    }
}