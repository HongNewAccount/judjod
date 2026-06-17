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

    public async Task<IActionResult> Index(string sortBy = "latest", string filter = "all")
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var allProjects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .Include(p => p.Favorites.Where(f => f.UserId == userId))
            .ToListAsync();

        var projects = allProjects;

        // Apply filter
        if (filter == "completed")
        {
            projects = projects.Where(p => p.Status == "Completed").ToList();
        }
        else if (filter == "notcompleted")
        {
            projects = projects.Where(p => p.Status != "Completed").ToList();
        }
        else if (filter == "inprogress")
        {
            projects = projects.Where(p => p.Status == "InProgress").ToList();
        }

        // Apply sorting
        projects = sortBy switch
        {
            "date_asc" => projects.OrderBy(p => p.StartDate).ToList(),
            "date_desc" => projects.OrderByDescending(p => p.StartDate).ToList(),
            "name_asc" => projects.OrderBy(p => p.Name).ToList(),
            "name_desc" => projects.OrderByDescending(p => p.Name).ToList(),
            "enddate_asc" => projects.OrderBy(p => p.EndDate ?? DateTime.MaxValue).ToList(),
            "enddate_desc" => projects.OrderByDescending(p => p.EndDate ?? DateTime.MinValue).ToList(),
            "favorites" => projects.OrderByDescending(p => p.Favorites.Count).ToList(),
            _ => projects.OrderByDescending(p => p.CreatedAt).ToList()
        };

        var stats = new Dictionary<string, int>
        {
            { "TotalProjects", allProjects.Count },
            { "ActiveProjects", allProjects.Count(p => p.Status == "InProgress") },
            { "OnHoldProjects", allProjects.Count(p => p.Status == "OnHold") },
            { "CompletedProjects", allProjects.Count(p => p.Status == "Completed") }
        };

        ViewBag.Stats = stats;
        ViewBag.CurrentSort = sortBy;
        ViewBag.CurrentFilter = filter;
        return View(projects);
    }

    public async Task<IActionResult> Details(int id)
    {
        var project = await _context.Projects
            .Include(p => p.CreatedByUser)
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        return View(project);
    }

    public async Task<IActionResult> Create()
    {
        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WebApplication1.Models.Project project, [FromForm] int[] ownerIds)
    {
        if (ModelState.IsValid)
        {
            project.CreatedByUserId = 1; // TODO: Get from session/user context

            // Add owners
            if (ownerIds != null && ownerIds.Length > 0)
            {
                foreach (var ownerId in ownerIds.Distinct())
                {
                    project.Owners.Add(new WebApplication1.Models.ProjectOwner
                    {
                        UserId = ownerId
                    });
                }
            }

            _context.Add(project);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(project);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var project = await _context.Projects
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (project == null)
            return NotFound();

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WebApplication1.Models.Project project, [FromForm] int[] ownerIds)
    {
        if (id != project.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingProject = await _context.Projects
                    .Include(p => p.Owners)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingProject != null)
                {
                    existingProject.Name = project.Name;
                    existingProject.Description = project.Description;
                    existingProject.StartDate = project.StartDate;
                    existingProject.EndDate = project.EndDate;
                    existingProject.Status = project.Status;
                    existingProject.Issues = project.Issues;
                    existingProject.UpdatedAt = DateTime.UtcNow;

                    // Update owners
                    _context.ProjectOwners.RemoveRange(existingProject.Owners);
                    if (ownerIds != null && ownerIds.Length > 0)
                    {
                        foreach (var ownerId in ownerIds.Distinct())
                        {
                            existingProject.Owners.Add(new WebApplication1.Models.ProjectOwner
                            {
                                ProjectId = id,
                                UserId = ownerId
                            });
                        }
                    }

                    _context.Update(existingProject);
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
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return NotFound();

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Project '{project.Name}' has been deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1; // TODO: Get from session
        var favorite = await _context.ProjectFavorites
            .FirstOrDefaultAsync(pf => pf.ProjectId == id && pf.UserId == userId);

        if (favorite != null)
        {
            _context.ProjectFavorites.Remove(favorite);
        }
        else
        {
            _context.ProjectFavorites.Add(new WebApplication1.Models.ProjectFavorite
            {
                ProjectId = id,
                UserId = userId
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
