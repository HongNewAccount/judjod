using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers;

public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var reports = await _context.Reports
            .Include(r => r.CreatedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        var upcomingEvents = await _context.OrganizationEvents
            .Where(e => e.EventDate >= DateTime.UtcNow)
            .OrderBy(e => e.EventDate)
            .Take(5)
            .ToListAsync();

        ViewBag.Projects = projects;
        ViewBag.RecentReports = reports;
        ViewBag.UpcomingEvents = upcomingEvents;

        return View();
    }
}
