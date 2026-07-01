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

    public async Task<IActionResult> Index(int? groupId)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

        var groups = await _context.ProjectGroups.OrderBy(g => g.Name).ToListAsync();
        ViewBag.Groups = groups;
        ViewBag.SelectedGroupId = groupId;

        var allProjects = await _context.Projects
            .Include(p => p.Group)
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

        // Board shows everything at once; only exclude archived (Closed) projects, sorted by SortOrder
        var projects = allProjects
            .Where(p => p.Status != "Closed")
            .Where(p => groupId == null || p.GroupId == groupId)
            .OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt).ToList();

        return View(projects);
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

    public async Task<IActionResult> Create(string? status)
    {
        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
        {
            TempData["ErrorMessage"] = "Your project management access has been suspended by an Admin.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        var groups = await _context.ProjectGroups.OrderBy(g => g.Name).ToListAsync();
        ViewBag.Users = users;
        ViewBag.Groups = groups;
        return View(new WebApplication1.Models.Project
        {
            Status = ValidBoardStatuses.Contains(status) ? status : "Planning",
            StartDate = DateTime.Today
        });
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

    private static readonly string[] ValidBoardStatuses = { "Planning", "InProgress", "Late", "Completed" };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
        {
            return BadRequest();
        }

        if (!ValidBoardStatuses.Contains(status))
        {
            return BadRequest();
        }

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var existingProject = await _context.Projects
            .Include(p => p.Owners)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (existingProject == null)
            return NotFound();

        bool isOwner = existingProject.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner)
        {
            return Forbid();
        }

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
        {
            return Forbid();
        }

        if (userRole == "Admin")
        {
            if (existingProject.Status != status)
            {
                _context.ActivityLogs.Add(new ActivityLog
                {
                    ProjectId = id,
                    UserId = userId,
                    ActionType = "StatusChanged",
                    Description = $"Project status changed from '{existingProject.Status}' to '{status}'",
                    OldValue = existingProject.Status,
                    NewValue = status,
                    CreatedAt = DateTime.UtcNow
                });

                existingProject.Status = status;
                existingProject.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, requiresApproval = false });
        }
        else
        {
            var existingPendingRequest = await _context.ProjectApprovalRequests
                .FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");

            var ownerIdsCsv = string.Join(",", existingProject.Owners.Select(o => o.UserId).Distinct());

            if (existingPendingRequest != null)
            {
                existingPendingRequest.Name = existingProject.Name;
                existingPendingRequest.Description = existingProject.Description;
                existingPendingRequest.StartDate = existingProject.StartDate;
                existingPendingRequest.EndDate = existingProject.EndDate;
                existingPendingRequest.Status = status;
                existingPendingRequest.Issues = existingProject.Issues;
                existingPendingRequest.OwnerIds = ownerIdsCsv;
                existingPendingRequest.RequestedAt = DateTime.UtcNow;
                existingPendingRequest.RequestedByUserId = userId;

                _context.Update(existingPendingRequest);
            }
            else
            {
                _context.ProjectApprovalRequests.Add(new ProjectApprovalRequest
                {
                    ProjectId = id,
                    RequestType = "Update",
                    Name = existingProject.Name,
                    Description = existingProject.Description,
                    StartDate = existingProject.StartDate,
                    EndDate = existingProject.EndDate,
                    Status = status,
                    Issues = existingProject.Issues,
                    OwnerIds = ownerIdsCsv,
                    RequestedByUserId = userId,
                    RequestedAt = DateTime.UtcNow,
                    ApprovalStatus = "Pending"
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, requiresApproval = true });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateName(int id, string name)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects.Include(p => p.Owners).FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner) return Forbid();
        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId)) return Forbid();

        if (userRole == "Admin")
        {
            _context.ActivityLogs.Add(new ActivityLog { ProjectId = id, UserId = userId, ActionType = "NameChanged", Description = $"Project renamed from '{project.Name}' to '{name}'", OldValue = project.Name, NewValue = name, CreatedAt = DateTime.UtcNow });
            project.Name = name.Trim();
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = false });
        }
        else
        {
            var pending = await _context.ProjectApprovalRequests.FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");
            var ownerIdsCsv = string.Join(",", project.Owners.Select(o => o.UserId).Distinct());
            if (pending != null) { pending.Name = name.Trim(); pending.RequestedAt = DateTime.UtcNow; pending.RequestedByUserId = userId; _context.Update(pending); }
            else { _context.ProjectApprovalRequests.Add(new ProjectApprovalRequest { ProjectId = id, RequestType = "Update", Name = name.Trim(), Description = project.Description, StartDate = project.StartDate, EndDate = project.EndDate, Status = project.Status, Issues = project.Issues, OwnerIds = ownerIdsCsv, RequestedByUserId = userId, RequestedAt = DateTime.UtcNow, ApprovalStatus = "Pending" }); }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = true });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePriority(int id, string priority)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var allowed = new[] { "None", "Low", "Medium", "High" };
        if (!allowed.Contains(priority)) return BadRequest();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects.Include(p => p.Owners).FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner) return Forbid();
        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId)) return Forbid();

        project.Priority = priority == "None" ? null : priority;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true, priority = project.Priority });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDueDate(int id, string? endDate)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects.Include(p => p.Owners).FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner) return Forbid();
        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId)) return Forbid();

        DateTime? newDate = string.IsNullOrWhiteSpace(endDate) ? null : (DateTime.TryParse(endDate, out var d) ? d : project.EndDate);

        if (userRole == "Admin")
        {
            _context.ActivityLogs.Add(new ActivityLog { ProjectId = id, UserId = userId, ActionType = "EndDateChanged", Description = $"End date changed to {newDate?.ToString("dd MMM yyyy") ?? "Ongoing"}", OldValue = project.EndDate?.ToString("dd MMM yyyy") ?? "Ongoing", NewValue = newDate?.ToString("dd MMM yyyy") ?? "Ongoing", CreatedAt = DateTime.UtcNow });
            project.EndDate = newDate;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = false, endDate = project.EndDate?.ToString("dd MMM") ?? "Ongoing" });
        }
        else
        {
            var pending = await _context.ProjectApprovalRequests.FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");
            var ownerIdsCsv = string.Join(",", project.Owners.Select(o => o.UserId).Distinct());
            if (pending != null) { pending.EndDate = newDate; pending.RequestedAt = DateTime.UtcNow; pending.RequestedByUserId = userId; _context.Update(pending); }
            else { _context.ProjectApprovalRequests.Add(new ProjectApprovalRequest { ProjectId = id, RequestType = "Update", Name = project.Name, Description = project.Description, StartDate = project.StartDate, EndDate = newDate, Status = project.Status, Issues = project.Issues, OwnerIds = ownerIdsCsv, RequestedByUserId = userId, RequestedAt = DateTime.UtcNow, ApprovalStatus = "Pending" }); }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = true });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOwners(int id, string ownerIds)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "Admin") return Forbid();

        var project = await _context.Projects.Include(p => p.Owners).FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        var newOwnerIds = (ownerIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s.Trim(), out var oid) ? oid : 0).Where(x => x > 0).Distinct().ToList();

        _context.ProjectOwners.RemoveRange(project.Owners);
        foreach (var oid in newOwnerIds)
        {
            project.Owners.Add(new ProjectOwner { ProjectId = id, UserId = oid });
        }
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderProjects(string orderedIds)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();

        if (string.IsNullOrEmpty(orderedIds)) return BadRequest();

        var ids = orderedIds.Split(',').Select(s => int.TryParse(s.Trim(), out var id) ? id : 0).Where(x => x > 0).ToArray();
        var projects = await _context.Projects.Where(p => ids.Contains(p.Id)).ToListAsync();
        for (int i = 0; i < ids.Length; i++)
        {
            var proj = projects.FirstOrDefault(p => p.Id == ids[i]);
            if (proj != null) proj.SortOrder = i;
        }
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ===== QUICK CREATE (inline popup) =====

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuick(string name, string status = "Planning",
        string? priority = null, string? endDate = null, string? ownerIds = null, int? groupId = null)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { error = "Name is required" });

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId))
            return Forbid();

        var ownerIdList = (ownerIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var oid) ? oid : 0).Where(x => x > 0).Distinct().ToList();

        var validStatus = ValidBoardStatuses.Contains(status) ? status : "Planning";
        DateTime? dueDate = string.IsNullOrWhiteSpace(endDate) ? null :
            (DateTime.TryParse(endDate, out var d) ? (DateTime?)d : null);

        if (userRole == "Admin")
        {
            var project = new WebApplication1.Models.Project
            {
                Name = name.Trim(),
                Status = validStatus,
                Priority = string.IsNullOrWhiteSpace(priority) || priority == "None" ? null : priority,
                StartDate = DateTime.Today,
                EndDate = dueDate,
                GroupId = groupId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Add(project);
            await _context.SaveChangesAsync();

            foreach (var oid in ownerIdList)
                _context.ProjectOwners.Add(new ProjectOwner { ProjectId = project.Id, UserId = oid });

            _context.ActivityLogs.Add(new ActivityLog
            {
                ProjectId = project.Id, UserId = userId, ActionType = "Created",
                Description = $"Project '{project.Name}' was created", CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = false, projectId = project.Id });
        }
        else
        {
            var request = new ProjectApprovalRequest
            {
                RequestType = "Create", Name = name.Trim(), Status = validStatus,
                StartDate = DateTime.Today, EndDate = dueDate,
                OwnerIds = ownerIdList.Any() ? string.Join(",", ownerIdList) : null,
                RequestedByUserId = userId, RequestedAt = DateTime.UtcNow, ApprovalStatus = "Pending"
            };
            _context.ProjectApprovalRequests.Add(request);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = true });
        }
    }

    // ===== TASK DETAIL (description + activity log) =====

    [HttpGet]
    public async Task<IActionResult> GetProjectDetails(int id)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var project = await _context.Projects
            .Include(p => p.Owners).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound();

        var logs = await _context.ActivityLogs
            .Where(al => al.ProjectId == id)
            .Include(al => al.User)
            .OrderByDescending(al => al.CreatedAt)
            .Take(20)
            .Select(al => new {
                al.ActionType, al.Description, al.OldValue, al.NewValue,
                CreatedAt = al.CreatedAt.ToString("dd MMM yyyy HH:mm"),
                UserName = al.User != null ? al.User.FirstName + " " + al.User.LastName : "Unknown"
            })
            .ToListAsync();

        return Ok(new {
            project.Id, project.Name,
            Description = project.Description ?? "",
            Logs = logs
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDescription(int id, string? description)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        var userRole = HttpContext.Session.GetString("UserRole");

        var project = await _context.Projects.Include(p => p.Owners).FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();

        bool isOwner = project.Owners.Any(o => o.UserId == userId);
        if (userRole != "Admin" && !isOwner) return Forbid();
        if (userRole != "Admin" && await IsProjectAccessSuspendedAsync(userId)) return Forbid();

        if (userRole == "Admin")
        {
            project.Description = description?.Trim();
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = false });
        }
        else
        {
            var pending = await _context.ProjectApprovalRequests
                .FirstOrDefaultAsync(r => r.ProjectId == id && r.RequestType == "Update" && r.ApprovalStatus == "Pending");
            var ownerIdsCsv = string.Join(",", project.Owners.Select(o => o.UserId).Distinct());
            if (pending != null)
            {
                pending.Description = description?.Trim();
                pending.RequestedAt = DateTime.UtcNow;
                pending.RequestedByUserId = userId;
                _context.Update(pending);
            }
            else
            {
                _context.ProjectApprovalRequests.Add(new ProjectApprovalRequest
                {
                    ProjectId = id, RequestType = "Update", Name = project.Name,
                    Description = description?.Trim(), StartDate = project.StartDate,
                    EndDate = project.EndDate, Status = project.Status, Issues = project.Issues,
                    OwnerIds = ownerIdsCsv, RequestedByUserId = userId,
                    RequestedAt = DateTime.UtcNow, ApprovalStatus = "Pending"
                });
            }
            await _context.SaveChangesAsync();
            return Ok(new { success = true, requiresApproval = true });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("ProjectTracker/DeleteAjax/{id:int}")]
    public async Task<IActionResult> DeleteAjax(int id)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();

        var project = await _context.Projects.FindAsync(id);
        if (project == null) return NotFound();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;
        _context.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = id, UserId = userId, ActionType = "Deleted",
            Description = $"Project '{project.Name}' was deleted", CreatedAt = DateTime.UtcNow
        });

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // ===== GROUP MANAGEMENT =====

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(string name, string? color)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest();

        var group = new ProjectGroup
        {
            Name = name.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.ProjectGroups.Add(group);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();

        var group = await _context.ProjectGroups.FindAsync(id);
        if (group == null) return NotFound();

        _context.ProjectGroups.Remove(group);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignGroup(int id, int? groupId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();

        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();

        var project = await _context.Projects.FindAsync(id);
        if (project == null) return NotFound();

        project.GroupId = groupId;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
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
