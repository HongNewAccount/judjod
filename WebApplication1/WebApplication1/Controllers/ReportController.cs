using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string searchTerm = "", string sortBy = "latest", string statusFilter = "all", int page = 1)
    {
        const int pageSize = 10;
        var reports = await _context.Reports
            .Include(r => r.CreatedByUser)
            .Include(r => r.Assignments)
            .ToListAsync();

        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            reports = reports.Where(r =>
                r.Title.ToLower().Contains(lowerSearchTerm) ||
                r.Description.ToLower().Contains(lowerSearchTerm) ||
                r.Location.ToLower().Contains(lowerSearchTerm)
            ).ToList();
        }

        // Status filter
        if (statusFilter != "all")
        {
            reports = reports.Where(r => r.Status == statusFilter).ToList();
        }

        // Sort
        reports = sortBy switch
        {
            "oldest" => reports.OrderBy(r => r.CreatedAt).ToList(),
            "title-asc" => reports.OrderBy(r => r.Title).ToList(),
            "title-desc" => reports.OrderByDescending(r => r.Title).ToList(),
            "priority-high" => reports.OrderByDescending(r => r.Priority == "High").ThenByDescending(r => r.CreatedAt).ToList(),
            _ => reports.OrderByDescending(r => r.CreatedAt).ToList()
        };

        // Statistics
        var stats = new Dictionary<string, int>
        {
            { "TotalReports", reports.Count },
            { "PendingReports", reports.Count(r => r.Status == "Pending") },
            { "InProgressReports", reports.Count(r => r.Status == "InProgress") },
            { "CompletedReports", reports.Count(r => r.Status == "Completed") }
        };

        var totalCount = reports.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));
        var pagedReports = reports.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Stats = stats;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.SortBy = sortBy;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(pagedReports);
    }

    public async Task<IActionResult> Details(int id)
    {
        var report = await _context.Reports
            .Include(r => r.CreatedByUser)
            .Include(r => r.Assignments)
                .ThenInclude(a => a.AssignedToUser)
            .Include(r => r.Comments)
                .ThenInclude(c => c.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
            return NotFound();

        return View(report);
    }

    public async Task<IActionResult> Create()
    {
        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Report report)
    {
        if (ModelState.IsValid)
        {
            report.CreatedByUserId = 1;
            report.ReportedDate = DateTime.UtcNow;
            report.CreatedAt = DateTime.UtcNow;
            _context.Add(report);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(report);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            return NotFound();

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Report report)
    {
        if (id != report.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingReport = await _context.Reports.FindAsync(id);
                if (existingReport != null)
                {
                    existingReport.Title = report.Title;
                    existingReport.Description = report.Description;
                    existingReport.Priority = report.Priority;
                    existingReport.Status = report.Status;
                    existingReport.ScheduledDate = report.ScheduledDate;
                    existingReport.ScheduledEndDate = report.ScheduledEndDate;
                    existingReport.Location = report.Location;
                    existingReport.IsOnSite = report.IsOnSite;
                    existingReport.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existingReport);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(report);
    }
}
