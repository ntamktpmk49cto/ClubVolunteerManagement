
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Controllers;

[Authorize]
public class EventRegistrationsController : Controller
{
    private readonly EventRegistrationService _service;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public EventRegistrationsController(
        EventRegistrationService service,
        UserManager<ApplicationUser> users,
        ApplicationDbContext db)
    {
        _service = service;
        _users = users;
        _db = db;
    }

    // POST: /EventRegistrations/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int eventId)
    {
        var userId = _users.GetUserId(User);

        if (userId == null)
            return Challenge();

        var result = await _service.RegisterAsync(
            eventId, userId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message;

        return RedirectToAction(
            "Details", "Events", new { id = eventId });
    }

    // POST: /EventRegistrations/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int eventId)
    {
        var userId = _users.GetUserId(User);

        if (userId == null)
            return Challenge();

        var result = await _service.CancelAsync(
            eventId, userId);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message;

        return RedirectToAction(
            "Details", "Events", new { id = eventId });
    }

    // GET: /EventRegistrations/Mine
    public async Task<IActionResult> Mine()
    {
        var userId = _users.GetUserId(User);

        var registrations = await _db.EventRegistrations
            .Include(x => x.Event)
                .ThenInclude(x => x.Club)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RegisteredAtUtc)
            .ToListAsync();

        return View(registrations);
    }
}
