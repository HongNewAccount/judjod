using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers;

public class ProjectTrackerController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProjectTrackerController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var projects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var stats = new Dictionary<string, int>
        {
            { "TotalProjects", projects.Count },
            { "ActiveProjects", projects.Count(p => p.Status == "InProgress") },
            { "CompletedProjects", projects.Count(p => p.Status == "Completed") },
            { "OnHoldProjects", projects.Count(p => p.Status == "OnHold") }
        };

        ViewBag.Stats = stats;
        return View(projects);
    }

    public async Task<IActionResult> Details(int id)
    {
        var project = await _context.Projects
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        return View(project);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WebApplication1.Models.Project project)
    {
        if (ModelState.IsValid)
        {
            project.CreatedByUserId = 1; // TODO: Get from session/user context
            _context.Add(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(project);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();

        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WebApplication1.Models.Project project)
    {
        if (id != project.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                project.UpdatedAt = DateTime.UtcNow;
                _context.Update(project);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        return View(project);
    }
}
