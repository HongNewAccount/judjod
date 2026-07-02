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
            .Where(p => p.EndDate.HasValue && p.EndDate.Value >= startDate && p.EndDate.Value <= endDate && p.Status != "Closed")
            .ToListAsync();

        var allProjects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.AllProjects = allProjects;
        ViewBag.MonthProjects = projects;
        ViewBag.SelectedYear = selectedYear;
        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.MonthName = startDate.ToString("MMMM yyyy");
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;

        return View();
    }
}
