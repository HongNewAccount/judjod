using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class ProjectTrackerController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProjectTrackerController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<bool> IsProjectAccessSuspendedAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        return user?.ProjectAccessSuspended ?? false;
    }

    public async Task<IActionResult> Index(string searchTerm = "", string sortBy = "latest", string filter = "all", int page = 1)
    {
        const int pageSize = 10;
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var allProjects = await _context.Projects
            .Include(p => p.CreatedByUser)
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .Include(p => p.Favorites)
            .ToListAsync();

        var pendingCreations = await _context.ProjectApprovalRequests
            .Include(r => r.RequestedByUser)
            .Where(r => r.RequestType == "Create" && r.ApprovalStatus == "Pending" && r.RequestedByUserId == userId)
            .ToListAsync();

        var pendingUpdates = await _context.ProjectApprovalRequests
            .Where(r => r.RequestType == "Update" && r.ApprovalStatus == "Pending")
            .Select(r => r.ProjectId)
            .ToListAsync();

        var pendingUpdateIds = new HashSet<int>(pendingUpdates.Where(id => id.HasValue).Select(id => id!.Value));

        ViewBag.PendingCreations = pendingCreations;
        ViewBag.PendingUpdates = pendingUpdateIds;

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
        else if (filter == "thismonth")
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            projects = projects.Where(p =>
                                          (p.StartDate.Date >= startOfMonth.Date && p.StartDate.Date <= endOfMonth.Date) ||
                                          (p.EndDate.HasValue && p.EndDate.Value.Date >= startOfMonth.Date && p.EndDate.Value.Date <= endOfMonth.Date)).ToList();
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

        // Apply search
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            projects = projects.Where(p =>
                p.Name.ToLower().Contains(lowerSearchTerm) ||
                p.Owners.Any(o => o.User != null &&
                    ($"{o.User.FirstName} {o.User.LastName}".ToLower().Contains(lowerSearchTerm)))
            ).ToList();
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

        var totalCount = projects.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));
        var pagedProjects = projects.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Stats = stats;
        ViewBag.CurrentSort = sortBy;
        ViewBag.CurrentFilter = filter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        return View(pagedProjects);
    }

    public async Task<IActionResult> Details(int id, int page = 1)
    {
        var project = await _context.Projects
            .Include(p => p.CreatedByUser)
            .Include(p => p.Owners)
                .ThenInclude(po => po.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return NotFound();

        var pendingUpdate = await _context.ProjectApprovalRequests
            .Include(r => r.RequestedByUser)
            .FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");

        ViewBag.PendingUpdate = pendingUpdate;

        const int pageSize = 10;
        var activityLogsQuery = _context.ActivityLogs
            .Where(al => al.ProjectId == id)
            .Include(al => al.User)
            .OrderByDescending(al => al.CreatedAt);

        var totalLogs = await activityLogsQuery.CountAsync();
        var paginatedLogs = await activityLogsQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalLogs / pageSize);

        ViewBag.TotalLogs = totalLogs;
        ViewBag.PaginatedLogs = paginatedLogs;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(project);
    }

    public async Task<IActionResult> Create()
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
        {
            TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WebApplication1.Models.Project project, [FromForm] int[] ownerIds)
    {
        var createUserId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var createUserRole = HttpContext.Session.GetString("UserRole");

        if (createUserRole != "Admin" && await IsProjectAccessSuspendedAsync(createUserId))
        {
            TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
            return RedirectToAction(nameof(Index));
        }

        if (ModelState.IsValid)
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
            var userRole = HttpContext.Session.GetString("UserRole");

            if (userRole == "Admin")
            {
                project.CreatedByUserId = userId;

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
                    UserId = userId,
                    ActionType = "Created",
                    Description = $"Project '{project.Name}' was created",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Project '{project.Name}' has been created successfully.";
            }
            else
            {
                var approvalRequest = new WebApplication1.Models.ProjectApprovalRequest
                {
                    RequestType = "Create",
                    Name = project.Name,
                    Description = project.Description,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    Status = project.Status ?? "Planning",
                    Issues = project.Issues,
                    OwnerIds = ownerIds != null ? string.Join(",", ownerIds.Distinct()) : null,
                    RequestedByUserId = userId,
                    RequestedAt = DateTime.UtcNow,
                    ApprovalStatus = "Pending"
                };

                _context.ProjectApprovalRequests.Add(approvalRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Your request to create project '{project.Name}' has been submitted for Admin approval.";
            }

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

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
        {
            TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
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
                var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
                var userRole = HttpContext.Session.GetString("UserRole");

                var existingProject = await _context.Projects
                    .Include(p => p.Owners)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (existingProject == null)
                    return NotFound();

                // Check permission: Admin or Owner only
                bool isOwner = existingProject.Owners.Any(o => o.UserId == userId);
                if (userRole != "Admin" && !isOwner)
                {
                    TempData["ErrorMessage"] = "You can only edit projects where you are an owner";
                    return RedirectToAction(nameof(Index));
                }

                if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
                {
                    TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
                    return RedirectToAction(nameof(Index));
                }

                if (userRole == "Admin")
                {
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

                    TempData["SuccessMessage"] = $"Project '{project.Name}' updated successfully.";
                }
                else
                {
                    // Create update request for non-admin
                    var existingPendingRequest = await _context.ProjectApprovalRequests
                        .FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");

                    if (existingPendingRequest != null)
                    {
                        existingPendingRequest.Name = project.Name;
                        existingPendingRequest.Description = project.Description;
                        existingPendingRequest.StartDate = project.StartDate;
                        existingPendingRequest.EndDate = project.EndDate;
                        existingPendingRequest.Status = project.Status;
                        existingPendingRequest.Issues = project.Issues;
                        existingPendingRequest.OwnerIds = ownerIds != null ? string.Join(",", ownerIds.Distinct()) : null;
                        existingPendingRequest.RequestedAt = DateTime.UtcNow;
                        existingPendingRequest.RequestedByUserId = userId;

                        _context.Update(existingPendingRequest);
                        TempData["SuccessMessage"] = $"Your pending edit request for project '{project.Name}' has been updated and is awaiting Admin approval.";
                    }
                    else
                    {
                        var approvalRequest = new WebApplication1.Models.ProjectApprovalRequest
                        {
                            ProjectId = id,
                            RequestType = "Update",
                            Name = project.Name,
                            Description = project.Description,
                            StartDate = project.StartDate,
                            EndDate = project.EndDate,
                            Status = project.Status,
                            Issues = project.Issues,
                            OwnerIds = ownerIds != null ? string.Join(",", ownerIds.Distinct()) : null,
                            RequestedByUserId = userId,
                            RequestedAt = DateTime.UtcNow,
                            ApprovalStatus = "Pending"
                        };

                        _context.ProjectApprovalRequests.Add(approvalRequest);
                        TempData["SuccessMessage"] = $"Your edit request for project '{project.Name}' has been submitted for Admin approval.";
                    }

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
    public async Task<IActionResult> CancelRequest(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var request = await _context.ProjectApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        // Check permission: Request creator or Admin
        if (request.RequestedByUserId != userId && userRole != "Admin")
        {
            TempData["ErrorMessage"] = "You do not have permission to cancel this request.";
            return RedirectToAction(nameof(Index));
        }

        if (request.ApprovalStatus != "Pending")
        {
            TempData["ErrorMessage"] = "Only pending requests can be cancelled.";
            return RedirectToAction(nameof(Index));
        }

        var projectName = request.Name;
        _context.ProjectApprovalRequests.Remove(request);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Approval request for '{projectName}' has been cancelled successfully.";
        return RedirectToAction(nameof(Index));
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

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
        {
            TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
            return RedirectToAction(nameof(Index));
        }

        var projectName = project.Name;

        if (userRole == "Admin")
        {
            // Admin can delete immediately
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
        }
        else
        {
            // Non-admin owner must request approval
            var approvalRequest = new WebApplication1.Models.ProjectApprovalRequest
            {
                ProjectId = id,
                RequestType = "Delete",
                Name = projectName,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                Status = project.Status,
                Issues = project.Issues,
                RequestedByUserId = userId,
                RequestedAt = DateTime.UtcNow,
                ApprovalStatus = "Pending"
            };

            _context.ProjectApprovalRequests.Add(approvalRequest);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Your request to delete project '{projectName}' has been submitted for Admin approval.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    public async Task<IActionResult> ActivityLog(string filter = "all", string searchTerm = "", string searchType = "all", string sortBy = "newest", int page = 1)
    {
        const int pageSize = 20;

        var query = _context.ActivityLogs as IQueryable<WebApplication1.Models.ActivityLog>;

        // Filter by action type
        if (filter != "all")
        {
            query = query.Where(al => al.ActionType == filter);
        }

        // Search by user or project
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            if (searchType == "user")
            {
                query = query.Where(al =>
                    al.User.FirstName.ToLower().Contains(lowerSearchTerm) ||
                    al.User.LastName.ToLower().Contains(lowerSearchTerm)
                );
            }
            else if (searchType == "project")
            {
                query = query.Where(al =>
                    al.Project.Name.ToLower().Contains(lowerSearchTerm)
                );
            }
            else
            {
                query = query.Where(al =>
                    al.User.FirstName.ToLower().Contains(lowerSearchTerm) ||
                    al.User.LastName.ToLower().Contains(lowerSearchTerm) ||
                    (al.Project != null && al.Project.Name.ToLower().Contains(lowerSearchTerm))
                );
            }
        }

        var queryWithIncludes = query
            .Include(al => al.Project)
            .Include(al => al.User);

        IOrderedQueryable<WebApplication1.Models.ActivityLog> sortedQuery = sortBy switch
        {
            "oldest" => queryWithIncludes.OrderBy(al => al.CreatedAt),
            "user" => queryWithIncludes.OrderBy(al => al.User.FirstName).ThenBy(al => al.User.LastName),
            "project" => queryWithIncludes.OrderBy(al => al.Project.Name),
            _ => queryWithIncludes.OrderByDescending(al => al.CreatedAt)
        };

        var totalCount = await sortedQuery.CountAsync();
        var logs = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        ViewBag.FilterType = filter;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.SearchType = searchType;
        ViewBag.SortBy = sortBy;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(logs);
    }
}
