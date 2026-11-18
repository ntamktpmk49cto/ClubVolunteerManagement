using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ClosedXML.Excel;

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

        //    ======
        // 1. SINH VIÊN THAM GIA CLB
        // POST: /ClubMembers/Join
        //    ======
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Join(int clubId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            // Bắt buộc điền đầy đủ tất cả thông tin hồ sơ trước khi đăng ký CLB
            bool isProfileIncomplete =
                string.IsNullOrWhiteSpace(user.FullName) ||
                string.IsNullOrWhiteSpace(user.StudentCode) ||
                string.IsNullOrWhiteSpace(user.SchoolOrFaculty) ||
                string.IsNullOrWhiteSpace(user.Major) ||
                string.IsNullOrWhiteSpace(user.Cohort) ||
                string.IsNullOrWhiteSpace(user.ClassName) ||
                string.IsNullOrWhiteSpace(user.PhoneNumber);

            if (isProfileIncomplete)
            {
                TempData["Error"] = "Vui lòng cập nhật đầy đủ toàn bộ thông tin hồ sơ sinh viên trước khi đăng ký tham gia CLB.";
                return Redirect("/Identity/Account/Manage");
            }
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

        //    ======
        // 2. DANH SÁCH CHỜ DUYỆT
        // GET: /ClubMembers/Pending?clubId=1
        //    ======
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

        //    ======
        // 3. DUYỆT THÀNH VIÊN
        // POST: /ClubMembers/Approve
        //    ======
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

        //    ======
        // 4. TỪ CHỐI THÀNH VIÊN
        // POST: /ClubMembers/Reject
        //    ======
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

        //    ======
        // 5. CLB CỦA TÔI
        // GET: /ClubMembers/MyClubs
        //    ======
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
        public async Task<IActionResult> Members(int clubId, string? search, string? cohort)
        {
            var club = await _context.Clubs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clubId);

            if (club == null)
                return NotFound();

            if (!await CanManageClubAsync(clubId))
                return Forbid();

            var query = _context.ClubMembers
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Approved);

            // Lọc theo từ khóa (Họ tên hoặc MSSV)
            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim().ToLower();
                query = query.Where(m =>
                    (m.User.FullName != null && m.User.FullName.ToLower().Contains(keyword)) ||
                    (m.User.StudentCode != null && m.User.StudentCode.ToLower().Contains(keyword)));
            }

            // Lọc theo Khóa (VD: K47, K48)
            if (!string.IsNullOrWhiteSpace(cohort))
            {
                query = query.Where(m => m.User.Cohort == cohort);
            }

            var members = await query
                .OrderBy(m => m.Role == ClubRole.Manager ? 0 : 1)
                .ThenBy(m => m.User.FullName)
                .ToListAsync();

            // Lấy danh sách các Khóa hiện có trong CLB để đổ vào dropdown lọc
            var cohorts = await _context.ClubMembers
                .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Approved && m.User.Cohort != null)
                .Select(m => m.User.Cohort!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewData["ClubId"] = clubId;
            ViewData["ClubName"] = club.Name;
            ViewData["CurrentSearch"] = search;
            ViewData["CurrentCohort"] = cohort;
            ViewBag.Cohorts = cohorts;

            return View(members);
        }

        // GET: /ClubMembers/ExportMembersExcel?clubId=1
        [HttpGet]
        public async Task<IActionResult> ExportMembersExcel(int clubId)
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
                .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Approved)
                .OrderBy(m => m.Role == ClubRole.Manager ? 0 : 1)
                .ThenBy(m => m.User.FullName)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DanhSachThanhVien");

            // Tiêu đề báo cáo
            worksheet.Cell(1, 1).Value = $"DANH SÁCH THÀNH VIÊN - {club.Name.ToUpper()}";
            worksheet.Range(1, 1, 1, 10).Merge().Style.Font.SetBold().Font.SetFontSize(14);

            // Header bảng
            string[] headers = {
        "STT", "Họ và tên", "MSSV", "Email", "Số điện thoại",
        "Trường / Khoa", "Ngành học", "Khóa", "Lớp", "Vai trò"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.SetBold();
                cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Đổ dữ liệu sinh viên
            int row = 4;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = m.User.FullName ?? m.User.UserName;
                worksheet.Cell(row, 3).Value = m.User.StudentCode ?? "";
                worksheet.Cell(row, 4).Value = m.User.Email ?? "";
                worksheet.Cell(row, 5).Value = m.User.PhoneNumber ?? "";
                worksheet.Cell(row, 6).Value = m.User.SchoolOrFaculty ?? "";
                worksheet.Cell(row, 7).Value = m.User.Major ?? "";
                worksheet.Cell(row, 8).Value = m.User.Cohort ?? "";
                worksheet.Cell(row, 9).Value = m.User.ClassName ?? "";
                worksheet.Cell(row, 10).Value = m.Role == ClubRole.Manager ? "Ban chủ nhiệm" : "Thành viên";

                worksheet.Range(row, 1, row, 10).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            string fileName = $"ThanhVien_CLB_{clubId}_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
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
        // GET: /ClubMembers/Stats?clubId=1
        [HttpGet]
        public async Task<IActionResult> Stats(int clubId)
        {
            var club = await _context.Clubs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clubId);
            if (club == null) return NotFound();

            if (!await CanManageClubAsync(clubId)) return Forbid();

            var members = await _context.ClubMembers
                .Include(m => m.User)
                .AsNoTracking()
                .Where(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Approved)
                .ToListAsync();

            // Thống kê theo Trường / Khoa
            var byFaculty = members
                .GroupBy(m => string.IsNullOrWhiteSpace(m.User.SchoolOrFaculty) ? "Chưa cập nhật" : m.User.SchoolOrFaculty)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            // Thống kê theo Khóa
            var byCohort = members
                .GroupBy(m => string.IsNullOrWhiteSpace(m.User.Cohort) ? "Chưa cập nhật" : m.User.Cohort)
                .OrderBy(g => g.Key)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            ViewData["ClubId"] = clubId;
            ViewData["ClubName"] = club.Name;
            ViewBag.TotalMembers = members.Count;
            ViewBag.PendingCount = await _context.ClubMembers.CountAsync(m => m.ClubId == clubId && m.Status == ClubMemberStatus.Pending);

            ViewBag.FacultyLabels = System.Text.Json.JsonSerializer.Serialize(byFaculty.Select(x => x.Label));
            ViewBag.FacultyData = System.Text.Json.JsonSerializer.Serialize(byFaculty.Select(x => x.Count));

            ViewBag.CohortLabels = System.Text.Json.JsonSerializer.Serialize(byCohort.Select(x => x.Label));
            ViewBag.CohortData = System.Text.Json.JsonSerializer.Serialize(byCohort.Select(x => x.Count));

            return View();
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
        // GET: /ClubMembers/MemberProfile?clubId=1&userId=...
        [HttpGet]
        public async Task<IActionResult> MemberProfile(int clubId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            // LỚP BẢO MẬT 1: Dùng sẵn helper của controller (chỉ Admin hoặc Ban chủ nhiệm đã được duyệt của CLB này)
            if (!await CanManageClubAsync(clubId))
                return Forbid();

            // LỚP BẢO MẬT 2: Chỉ cho xem nếu sinh viên này thực sự thuộc/đăng ký vào đúng clubId này
            var membership = await _context.ClubMembers
                .Include(m => m.User)
                .Include(m => m.Club)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == userId);

            if (membership == null || membership.User == null)
                return NotFound("Không tìm thấy hồ sơ sinh viên này trong danh sách của câu lạc bộ.");

            return View(membership);
        }


    }
}