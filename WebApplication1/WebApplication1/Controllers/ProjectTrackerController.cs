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
            .Include(p => p.Favorites)
            .ToListAsync();

        // Auto-convert to Late if past end date and not completed
        foreach (var project in allProjects)
        {
            if (project.EndDate.HasValue &&
                DateTime.UtcNow.Date > project.EndDate.Value.Date &&
                project.Status != "Completed")
            {
                project.Status = "Late";
            }
        }
        await _context.SaveChangesAsync();

        var projects = allProjects;

        // Apply filter
        if (filter == "completed")
        {
            projects = projects.Where(p => p.Status == "Completed").ToList();
        }
        else if (filter == "late")
        {
            projects = projects.Where(p => p.Status == "Late").ToList();
        }
        else if (filter == "notcompleted")
        {
            projects = projects.Where(p => p.Status != "Completed" && p.Status != "Closed").ToList();
        }
        else if (filter == "inprogress")
        {
            projects = projects.Where(p => p.Status == "InProgress").ToList();
        }
        else if (filter == "favorites")
        {
            projects = projects.Where(p => p.Favorites.Any()).ToList();
        }

        // Count stats before removing Closed projects
        var stats = new Dictionary<string, int>
        {
            { "TotalProjects", allProjects.Where(p => p.Status != "Closed").Count() },
            { "ActiveProjects", allProjects.Count(p => p.Status == "InProgress") },
            { "PlanningProjects", allProjects.Count(p => p.Status == "Planning") },
            { "LateProjects", allProjects.Count(p => p.Status == "Late") },
            { "CompletedProjects", allProjects.Count(p => p.Status == "Completed") },
            { "FavoriteProjects", allProjects.Count(p => p.Favorites.Any()) }
        };

        // Remove Closed projects from view
        projects = projects.Where(p => p.Status != "Closed").ToList();

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
            .Include(p => p.ActivityLogs)
                .ThenInclude(al => al.User)
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
            project.CreatedByUserId = HttpContext.Session.GetInt32("UserId") ?? 1;

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

            // Log activity
            _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
            {
                ProjectId = project.Id,
                UserId = project.CreatedByUserId,
                ActionType = "Created",
                Description = $"Project '{project.Name}' was created",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View(project);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        // Check permission: Admin or Owner only
        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner)
        {
            TempData["ErrorMessage"] = "You can only edit projects where you are an owner";
            return RedirectToAction(nameof(Index));
        }

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
                    int userId = HttpContext.Session.GetInt32("UserId") ?? 1;

                    // Track changes
                    if (existingProject.Status != project.Status)
                    {
                        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                        {
                            ProjectId = id,
                            UserId = userId,
                            ActionType = "StatusChanged",
                            Description = $"Project status changed from '{existingProject.Status}' to '{project.Status}'",
                            OldValue = existingProject.Status,
                            NewValue = project.Status,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (existingProject.Name != project.Name)
                    {
                        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                        {
                            ProjectId = id,
                            UserId = userId,
                            ActionType = "NameChanged",
                            Description = $"Project name changed from '{existingProject.Name}' to '{project.Name}'",
                            OldValue = existingProject.Name,
                            NewValue = project.Name,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (existingProject.Description != project.Description)
                    {
                        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                        {
                            ProjectId = id,
                            UserId = userId,
                            ActionType = "DescriptionChanged",
                            Description = "Project description was updated",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (existingProject.StartDate != project.StartDate)
                    {
                        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                        {
                            ProjectId = id,
                            UserId = userId,
                            ActionType = "StartDateChanged",
                            Description = $"Project start date changed to {project.StartDate:MMM dd, yyyy}",
                            OldValue = existingProject.StartDate.ToString("MMM dd, yyyy"),
                            NewValue = project.StartDate.ToString("MMM dd, yyyy"),
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (existingProject.EndDate != project.EndDate)
                    {
                        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                        {
                            ProjectId = id,
                            UserId = userId,
                            ActionType = "EndDateChanged",
                            Description = $"Project end date changed to {project.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing"}",
                            OldValue = existingProject.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing",
                            NewValue = project.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    existingProject.Name = project.Name;
                    existingProject.Description = project.Description;
                    existingProject.StartDate = project.StartDate;
                    existingProject.EndDate = project.EndDate;
                    existingProject.Status = project.Status;
                    existingProject.Issues = project.Issues;
                    existingProject.UpdatedAt = DateTime.UtcNow;

                    // Update owners
                    var oldOwnerIds = existingProject.Owners.Select(o => o.UserId).ToList();
                    var newOwnerIds = ownerIds ?? Array.Empty<int>();

                    if (!oldOwnerIds.SequenceEqual(newOwnerIds))
                    {
                        var addedOwners = newOwnerIds.Except(oldOwnerIds).ToList();
                        var removedOwners = oldOwnerIds.Except(newOwnerIds).ToList();

                        _context.ProjectOwners.RemoveRange(existingProject.Owners);
                        foreach (var ownerId in newOwnerIds.Distinct())
                        {
                            existingProject.Owners.Add(new WebApplication1.Models.ProjectOwner
                            {
                                ProjectId = id,
                                UserId = ownerId
                            });
                        }

                        if (addedOwners.Any() || removedOwners.Any())
                        {
                            var ownerNames = await _context.Users
                                .Where(u => addedOwners.Contains(u.Id) || removedOwners.Contains(u.Id))
                                .ToListAsync();

                            if (addedOwners.Any())
                            {
                                var addedNames = ownerNames.Where(u => addedOwners.Contains(u.Id)).Select(u => $"{u.FirstName} {u.LastName}").ToList();
                                _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                                {
                                    ProjectId = id,
                                    UserId = userId,
                                    ActionType = "OwnerAdded",
                                    Description = $"Owners added: {string.Join(", ", addedNames)}",
                                    CreatedAt = DateTime.UtcNow
                                });
                            }

                            if (removedOwners.Any())
                            {
                                var removedNames = ownerNames.Where(u => removedOwners.Contains(u.Id)).Select(u => $"{u.FirstName} {u.LastName}").ToList();
                                _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
                                {
                                    ProjectId = id,
                                    UserId = userId,
                                    ActionType = "OwnerRemoved",
                                    Description = $"Owners removed: {string.Join(", ", removedNames)}",
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
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
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects
            .Include(p => p.Owners)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        // Check permission: Admin or Owner only
        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner)
        {
            TempData["ErrorMessage"] = "You can only delete projects where you are an owner";
            return RedirectToAction(nameof(Index));
        }

        var projectName = project.Name;

        // Log the deletion before removing
        _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
        {
            ProjectId = id,
            UserId = userId,
            ActionType = "Deleted",
            Description = $"Project '{projectName}' was deleted",
            CreatedAt = DateTime.UtcNow
        });

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Project '{projectName}' has been deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleFavorite(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");

        // Only Admin can toggle favorites
        if (userRole != "Admin")
        {
            return Forbid();
        }

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var favorite = await _context.ProjectFavorites
            .FirstOrDefaultAsync(pf => pf.ProjectId == id && pf.UserId == userId);

        var isFavorited = false;

        if (favorite != null)
        {
            _context.ProjectFavorites.Remove(favorite);
            _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
            {
                ProjectId = id,
                UserId = userId,
                ActionType = "FavoriteRemoved",
                Description = "Project removed from favorites",
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            _context.ProjectFavorites.Add(new WebApplication1.Models.ProjectFavorite
            {
                ProjectId = id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            _context.ActivityLogs.Add(new WebApplication1.Models.ActivityLog
            {
                ProjectId = id,
                UserId = userId,
                ActionType = "FavoriteAdded",
                Description = "Project added to favorites",
                CreatedAt = DateTime.UtcNow
            });
            isFavorited = true;
        }

        await _context.SaveChangesAsync();

        // Return JSON for AJAX request
        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Ok(new { success = true, isFavorited });
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ActivityLog(string filter = "all")
    {
        var logs = await _context.ActivityLogs
            .Include(al => al.Project)
            .Include(al => al.User)
            .OrderByDescending(al => al.CreatedAt)
            .ToListAsync();

        if (filter != "all")
        {
            logs = logs.Where(al => al.ActionType == filter).ToList();
        }

        ViewBag.FilterType = filter;
        return View(logs);
    }
}
