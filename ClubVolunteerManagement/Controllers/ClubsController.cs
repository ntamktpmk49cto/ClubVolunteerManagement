
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers
{
    public class ClubsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClubsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Clubs
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var clubs = await _context.Clubs
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(clubs);
        }

        // GET: /Clubs/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var club = await _context.Clubs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (club == null)
                return NotFound();

            return View(club);
        }

     
        // GET: /Clubs/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Clubs/Create
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Description,LogoUrl")] Club input)
        {
            if (!ModelState.IsValid)
                return View(input);

            var club = new Club
            {
                Name = input.Name.Trim(),
                Description = input.Description,
                LogoUrl = input.LogoUrl,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Clubs.Add(club);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thêm CLB thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Clubs/Edit/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var club = await _context.Clubs.FindAsync(id);

            if (club == null)
                return NotFound();

            return View(club);
        }

        // POST: /Clubs/Edit/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Name,Description,LogoUrl")] Club input)
        {
            if (!ModelState.IsValid)
            {
                input.Id = id;
                return View(input);
            }

            var club = await _context.Clubs.FindAsync(id);

            if (club == null)
                return NotFound();

            club.Name = input.Name.Trim();
            club.Description = input.Description;
            club.LogoUrl = input.LogoUrl;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật CLB thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Clubs/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var club = await _context.Clubs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (club == null)
                return NotFound();

            return View(club);
        }

        // POST: /Clubs/Delete/5
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var club = await _context.Clubs.FindAsync(id);

            if (club == null)
                return NotFound();

            bool hasMembers = await _context.ClubMembers
                .AnyAsync(x => x.ClubId == id);

            bool hasEvents = await _context.Events
                .AnyAsync(x => x.ClubId == id);

            if (hasMembers || hasEvents)
            {
                TempData["Error"] =
                    "Không thể xóa CLB đã có thành viên hoặc sự kiện.";

                return RedirectToAction(nameof(Index));
            }

            _context.Clubs.Remove(club);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa CLB thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
