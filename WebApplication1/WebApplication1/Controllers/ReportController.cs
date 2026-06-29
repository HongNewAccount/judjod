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

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 20;

        var query = _context.Reports
            .Include(r => r.CreatedByUser)
            .Include(r => r.Assignments)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync();
        var reports = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(reports);
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
                report.UpdatedAt = DateTime.UtcNow;
                _context.Update(report);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        return View(report);
    }
}
