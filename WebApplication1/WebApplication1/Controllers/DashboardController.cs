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

    public async Task<IActionResult> Index(int? year, int? month)
    {
        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;

        var startDate = new DateTime(selectedYear, selectedMonth, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var projects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .Include(p => p.Owners)
            .Where(p => (p.EndDate.HasValue && p.EndDate.Value >= startDate && p.EndDate.Value <= endDate))
            .ToListAsync();

        var allProjects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var reports = await _context.Reports
            .Include(r => r.CreatedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        var events = await _context.OrganizationEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Attendees)
            .Where(e => (e.EventDate >= startDate && e.EventDate <= endDate) ||
                        (e.EventEndDate.HasValue && e.EventEndDate.Value >= startDate && e.EventEndDate.Value <= endDate) ||
                        (e.EventDate <= startDate && e.EventEndDate >= endDate))
            .ToListAsync();

        var monthReports = await _context.Reports
            .Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate)
            .ToListAsync();

        ViewBag.AllProjects = allProjects;
        ViewBag.MonthProjects = projects;
        ViewBag.MonthReports = monthReports;
        ViewBag.RecentReports = reports;
        ViewBag.SelectedYear = selectedYear;
        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.MonthName = startDate.ToString("MMMM yyyy");
        ViewBag.Events = events;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;

        return View();
    }
}
