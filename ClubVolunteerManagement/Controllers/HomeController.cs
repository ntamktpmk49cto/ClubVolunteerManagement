using System.Diagnostics;
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
        ViewBag.TotalClubs = await _context.Clubs.CountAsync();
        ViewBag.TotalEvents = await _context.Events
            .CountAsync(e => e.Status == EventStatus.Published || e.Status == EventStatus.Completed);
        ViewBag.TotalVolunteerHours = await _context.VolunteerHours
            .SumAsync(v => (decimal?)v.Hours) ?? 0m;

        var upcomingEvents = await _context.Events
            .Include(e => e.Club)
            .Where(e => e.Status == EventStatus.Published && e.EndAtUtc >= DateTime.UtcNow)
            .OrderBy(e => e.StartAtUtc)
            .Take(6)
            .ToListAsync();

        if (!upcomingEvents.Any())
        {
            upcomingEvents = await _context.Events
                .Include(e => e.Club)
                .Where(e => e.Status == EventStatus.Published)
                .OrderByDescending(e => e.StartAtUtc)
                .Take(6)
                .ToListAsync();
        }

        return View(upcomingEvents);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}