
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public EventsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    // Lấy danh sách CLB người dùng được quản lý.
    private async Task<List<Club>> GetManagedClubsAsync()
    {
        if (User.IsInRole("Admin"))
        {
            return await _db.Clubs
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        var userId = _users.GetUserId(User);

        if (userId == null)
            return new List<Club>();

        return await _db.ClubMembers
            .Where(x =>
                x.UserId == userId &&
                x.Status == ClubMemberStatus.Approved &&
                x.Role == ClubRole.Manager)
            .Select(x => x.Club)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private async Task<bool> CanManageAsync(int clubId)
    {
        if (User.IsInRole("Admin"))
            return true;

        var userId = _users.GetUserId(User);

        if (userId == null)
            return false;

        return await _db.ClubMembers.AnyAsync(x =>
            x.ClubId == clubId &&
            x.UserId == userId &&
            x.Status == ClubMemberStatus.Approved &&
            x.Role == ClubRole.Manager);
    }

    private async Task LoadClubOptionsAsync(
        EventFormViewModel model)
    {
        model.ClubOptions = (await GetManagedClubsAsync())
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToList();
    }

    // Thời gian nhập theo Việt Nam UTC+7.
    private static DateTime ToUtc(DateTime local)
    {
        return DateTime.SpecifyKind(
            local.AddHours(-7), DateTimeKind.Utc);
    }

    private static DateTime ToVietnamTime(DateTime utc)
    {
        return utc.AddHours(7);
    }


    private void ValidateEvent(EventFormViewModel model)
    {
        // Thời gian hiện tại tại Việt Nam.
        DateTime nowVN = DateTime.UtcNow.AddHours(7);

        // Kiểm tra thời gian bắt đầu.
        if (model.StartAtLocal <= nowVN)
        {
            ModelState.AddModelError(
                nameof(model.StartAtLocal),
                "Thời gian bắt đầu phải lớn hơn thời gian hiện tại.");
        }

        // Kiểm tra thời gian kết thúc.
        if (model.EndAtLocal <= nowVN)
        {
            ModelState.AddModelError(
                nameof(model.EndAtLocal),
                "Thời gian kết thúc không được ở quá khứ.");
        }

        if (model.EndAtLocal <= model.StartAtLocal)
        {
            ModelState.AddModelError(
                nameof(model.EndAtLocal),
                "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        // Kiểm tra hạn đăng ký.
        if (model.RegistrationDeadlineLocal <= nowVN)
        {
            ModelState.AddModelError(
                nameof(model.RegistrationDeadlineLocal),
                "Hạn đăng ký phải lớn hơn thời gian hiện tại.");
        }

        if (model.RegistrationDeadlineLocal >
            model.StartAtLocal)
        {
            ModelState.AddModelError(
                nameof(model.RegistrationDeadlineLocal),
                "Hạn đăng ký không được sau thời gian bắt đầu.");
        }

        // Kiểm tra giờ tình nguyện.
        if (model.Type == EventType.Volunteer &&
            model.VolunteerHours <= 0)
        {
            ModelState.AddModelError(
                nameof(model.VolunteerHours),
                "Hoạt động tình nguyện phải có số giờ lớn hơn 0.");
        }
    }


    // GET: /Events
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var clubs = User.Identity?.IsAuthenticated == true
            ? await GetManagedClubsAsync()
            : new List<Club>();

        var managedIds = clubs.Select(x => x.Id).ToHashSet();
        bool isAdmin = User.IsInRole("Admin");

        var events = await _db.Events
            .Include(x => x.Club)
            .AsNoTracking()
            .Where(x =>
                isAdmin ||
                managedIds.Contains(x.ClubId) ||
                x.Status == EventStatus.Published ||
                x.Status == EventStatus.InProgress ||
                x.Status == EventStatus.Completed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        return View(new EventsIndexViewModel
        {
            Events = events,
            ManagedClubIds = managedIds,
            IsAdmin = isAdmin
        });
    }

    // GET: /Events/Details/5
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var eventItem = await _db.Events
            .Include(x => x.Club)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (eventItem == null)
            return NotFound();

        bool canManage =
            User.Identity?.IsAuthenticated == true &&
            await CanManageAsync(eventItem.ClubId);

        bool isPublic =
            eventItem.Status == EventStatus.Published ||
            eventItem.Status == EventStatus.InProgress ||
            eventItem.Status == EventStatus.Completed;

        if (!isPublic && !canManage)
            return NotFound();

        var userId = _users.GetUserId(User);

        var myStatus = userId == null
            ? null
            : await _db.EventRegistrations
                .Where(x => x.EventId == id &&
                            x.UserId == userId)
                .Select(x => (RegistrationStatus?)x.Status)
                .FirstOrDefaultAsync();

        return View(new EventDetailsViewModel
        {
            Event = eventItem,
            ConfirmedCount = await _db.EventRegistrations
                .CountAsync(x =>
                    x.EventId == id &&
                    x.Status == RegistrationStatus.Confirmed),
            MyRegistrationStatus = myStatus,
            CanManage = canManage,
            IsAdmin = User.IsInRole("Admin")
        });
    }

    // GET: /Events/Create
    [Authorize]
    public async Task<IActionResult> Create()
    {
        var model = new EventFormViewModel();

        await LoadClubOptionsAsync(model);

        if (!model.ClubOptions.Any())
            return Forbid();

        return View(model);
    }

    // POST: /Events/Create
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EventFormViewModel model)
    {
        await LoadClubOptionsAsync(model);
        ValidateEvent(model);

        if (!await CanManageAsync(model.ClubId) ||
            !model.ClubOptions.Any(x =>
                x.Value == model.ClubId.ToString()))
            return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        var eventItem = new Event
        {
            ClubId = model.ClubId,
            Title = model.Title.Trim(),
            Description = model.Description,
            Location = model.Location.Trim(),
            Type = model.Type,
            Status = EventStatus.Draft,
            StartAtUtc = ToUtc(model.StartAtLocal),
            EndAtUtc = ToUtc(model.EndAtLocal),
            RegistrationDeadlineUtc =
                ToUtc(model.RegistrationDeadlineLocal),
            Capacity = model.Capacity,
            VolunteerHours =
                model.Type == EventType.Volunteer
                    ? model.VolunteerHours : 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Events.Add(eventItem);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã tạo bản nháp sự kiện";
        return RedirectToAction(nameof(Index));
    }


    // GET: /Events/Edit/5
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Events.FindAsync(id);

        if (e == null)
            return NotFound();

        // Kiểm tra quyền quản lý CLB.
        if (!await CanManageAsync(e.ClubId))
            return Forbid();

        // Cho phép sửa bản nháp hoặc sự kiện đã duyệt.
        bool canEdit =
            e.Status == EventStatus.Draft ||
            e.Status == EventStatus.Published;

        if (!canEdit)
        {
            TempData["Error"] =
                "Không thể sửa sự kiện ở trạng thái hiện tại.";

            return RedirectToAction(nameof(Index));
        }

        // Không cho sửa sự kiện đã bắt đầu.
        if (e.StartAtUtc <= DateTime.UtcNow)
        {
            TempData["Error"] =
                "Sự kiện đã bắt đầu hoặc đã kết thúc, không thể chỉnh sửa.";

            return RedirectToAction(nameof(Index));
        }

        // Chuyển dữ liệu sự kiện sang ViewModel.
        var model = new EventFormViewModel
        {
            Id = e.Id,
            ClubId = e.ClubId,
            Title = e.Title,
            Description = e.Description,
            Location = e.Location,
            Type = e.Type,

            StartAtLocal =
                ToVietnamTime(e.StartAtUtc),

            EndAtLocal =
                ToVietnamTime(e.EndAtUtc),

            RegistrationDeadlineLocal =
                ToVietnamTime(e.RegistrationDeadlineUtc),

            Capacity = e.Capacity,
            VolunteerHours = e.VolunteerHours
        };

        await LoadClubOptionsAsync(model);

        return View(model);
    }


    // POST: /Events/Edit/5
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, EventFormViewModel model)
    {
        // Dùng transaction để việc kiểm tra số chỗ
        // không xung đột với sinh viên đăng ký đồng thời.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        // Khóa bản ghi sự kiện trong lúc cập nhật.
        var e = await _db.Events
            .FromSqlInterpolated(
                $"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .FirstOrDefaultAsync();

        if (e == null)
            return NotFound();

        // Kiểm tra quyền người chỉnh sửa.
        if (!await CanManageAsync(e.ClubId))
            return Forbid();

        // Chỉ cho sửa Draft hoặc Published.
        if (e.Status != EventStatus.Draft &&
            e.Status != EventStatus.Published)
        {
            TempData["Error"] =
                "Không thể sửa sự kiện ở trạng thái hiện tại.";

            return RedirectToAction(nameof(Index));
        }

        // Không cho sửa sự kiện đã bắt đầu.
        if (e.StartAtUtc <= DateTime.UtcNow)
        {
            TempData["Error"] =
                "Sự kiện đã bắt đầu, không thể chỉnh sửa.";

            return RedirectToAction(nameof(Index));
        }

        // Không cho thay đổi CLB tổ chức.
        model.Id = id;
        model.ClubId = e.ClubId;
        ModelState.Remove(nameof(model.ClubId));

        // Kiểm tra ngày giờ.
        ValidateEvent(model);

        // Đếm số sinh viên đã đăng ký thành công.
        int confirmedCount =
            await _db.EventRegistrations.CountAsync(x =>
                x.EventId == id &&
                x.Status == RegistrationStatus.Confirmed);

        // Không được giảm sức chứa dưới số đã đăng ký.
        if (model.Capacity < confirmedCount)
        {
            ModelState.AddModelError(
                nameof(model.Capacity),
                $"Đã có {confirmedCount} sinh viên đăng ký. " +
                $"Số lượng tối đa không được nhỏ hơn {confirmedCount}.");
        }

        // Không cho đổi loại sự kiện đã công bố
        // nếu đã có sinh viên đăng ký.
        if (e.Status == EventStatus.Published &&
            confirmedCount > 0 &&
            model.Type != e.Type)
        {
            ModelState.AddModelError(
                nameof(model.Type),
                "Không thể đổi loại sự kiện đã có người đăng ký.");
        }

        if (!ModelState.IsValid)
        {
            await LoadClubOptionsAsync(model);
            return View(model);
        }

        // Cập nhật thông tin sự kiện.
        e.Title = model.Title.Trim();
        e.Description = model.Description;
        e.Location = model.Location.Trim();
        e.Type = model.Type;

        // Chuyển thời gian Việt Nam về UTC.
        e.StartAtUtc =
            ToUtc(model.StartAtLocal);

        e.EndAtUtc =
            ToUtc(model.EndAtLocal);

        e.RegistrationDeadlineUtc =
            ToUtc(model.RegistrationDeadlineLocal);

        e.Capacity = model.Capacity;

        e.VolunteerHours =
            model.Type == EventType.Volunteer
                ? model.VolunteerHours
                : 0;

        // Không đổi trạng thái:
        // Draft vẫn Draft, Published vẫn Published.

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] =
            "Cập nhật thông tin sự kiện thành công!";

        return RedirectToAction(nameof(Index));
    }


    // Gửi bản nháp chờ Admin duyệt.
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var e = await _db.Events.FindAsync(id);

        if (e == null)
            return NotFound();

        if (!await CanManageAsync(e.ClubId))
            return Forbid();

        if (e.Status != EventStatus.Draft)
            return BadRequest("Trạng thái không hợp lệ");

        if (e.StartAtUtc <= DateTime.UtcNow ||
            e.RegistrationDeadlineUtc <= DateTime.UtcNow)
            return BadRequest("Sự kiện đã quá hạn");

        e.Status = EventStatus.PendingApproval;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã gửi sự kiện chờ duyệt";
        return RedirectToAction(nameof(Index));
    }

    // Chỉ Admin được duyệt sự kiện.
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var e = await _db.Events.FindAsync(id);

        if (e == null)
            return NotFound();

        if (e.Status != EventStatus.PendingApproval)
            return BadRequest("Sự kiện không chờ duyệt");

        if (e.StartAtUtc <= DateTime.UtcNow ||
            e.RegistrationDeadlineUtc <= DateTime.UtcNow)
            return BadRequest("Sự kiện đã quá hạn");

        e.Status = EventStatus.Published;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Duyệt sự kiện thành công";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Events/CancelEvent/5
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEvent(int id)
    {
        var e = await _db.Events
            .Include(x => x.Registrations)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (e == null)
            return NotFound();

        // Chỉ Admin hoặc Ban chủ nhiệm CLB có quyền.
        if (!await CanManageAsync(e.ClubId))
            return Forbid();

        // Chỉ được hủy sự kiện đã công bố,
        // chưa bắt đầu diễn ra.
        if (e.Status != EventStatus.Published ||
            e.StartAtUtc <= DateTime.UtcNow)
        {
            TempData["Error"] =
                "Không thể hủy sự kiện ở trạng thái hiện tại.";

            return RedirectToAction(nameof(Index));
        }

        // Chuyển sự kiện sang trạng thái đã hủy.
        e.Status = EventStatus.Cancelled;

        // Hủy các đăng ký còn hiệu lực.
        foreach (var registration in e.Registrations)
        {
            registration.Status = RegistrationStatus.Cancelled;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] =
            "Đã hủy sự kiện thành công!";

        return RedirectToAction(nameof(Index));
    }

}
