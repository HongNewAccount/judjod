using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> PendingRequests()
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin" && userRole != "Editor")
        {
            return Forbid();
        }

        var requests = await _context.ProjectApprovalRequests
            .Include(r => r.RequestedByUser)
            .Include(r => r.Project)
            .Where(r => r.ApprovalStatus == "Pending")
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin" && userRole != "Editor")
            return Forbid();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

        var request = await _context.ProjectApprovalRequests
            .Include(r => r.RequestedByUser)
            .Include(r => r.Project)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        if (request.ApprovalStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(PendingRequests));
        }

        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                if (request.RequestType == "Create")
                {
                    var project = new Project
                    {
                        Name = request.Name,
                        Description = request.Description,
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        Status = request.Status ?? "Planning",
                        Issues = request.Issues,
                        CreatedByUserId = request.RequestedByUserId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Projects.Add(project);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(request.OwnerIds))
                    {
                        var ownerIds = request.OwnerIds.Split(',')
                            .Select(s => int.TryParse(s, out var parsedId) ? parsedId : 0)
                            .Where(x => x > 0)
                            .Distinct()
                            .ToList();

                        foreach (var ownerId in ownerIds)
                        {
                            _context.ProjectOwners.Add(new ProjectOwner
                            {
                                ProjectId = project.Id,
                                UserId = ownerId
                            });
                        }
                    }

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        ProjectId = project.Id,
                        UserId = userId, // Approved by Admin
                        ActionType = "Created",
                        Description = $"Project '{project.Name}' was created (Approved by Admin)",
                        CreatedAt = DateTime.UtcNow
                    });

                    request.ApprovalStatus = "Approved";
                    request.ApprovedByUserId = userId;
                    request.ApprovedAt = DateTime.UtcNow;
                }
                else if (request.RequestType == "Delete" && request.ProjectId.HasValue)
                {
                    var project = await _context.Projects
                        .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value);

                    if (project == null)
                    {
                        TempData["ErrorMessage"] = "The target project no longer exists.";
                        request.ApprovalStatus = "Rejected";
                        request.RejectionReason = "Target project no longer exists.";
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return RedirectToAction(nameof(PendingRequests));
                    }

                    var projectName = project.Name;

                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        ProjectId = project.Id,
                        UserId = userId,
                        ActionType = "Deleted",
                        Description = $"Project '{projectName}' was deleted (Approved by Admin)",
                        CreatedAt = DateTime.UtcNow
                    });

                    _context.Projects.Remove(project);

                    request.ApprovalStatus = "Approved";
                    request.ApprovedByUserId = userId;
                    request.ApprovedAt = DateTime.UtcNow;
                }
                else if (request.RequestType == "Update" && request.ProjectId.HasValue)
                {
                    var project = await _context.Projects
                        .Include(p => p.Owners)
                        .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value);

                    if (project == null)
                    {
                        TempData["ErrorMessage"] = "The target project no longer exists.";
                        request.ApprovalStatus = "Rejected";
                        request.RejectionReason = "Target project no longer exists.";
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return RedirectToAction(nameof(PendingRequests));
                    }

                    if (project.Status != request.Status)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            ProjectId = project.Id,
                            UserId = userId,
                            ActionType = "StatusChanged",
                            Description = $"Project status changed from '{project.Status}' to '{request.Status}' (Approved by Admin)",
                            OldValue = project.Status,
                            NewValue = request.Status,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (project.Name != request.Name)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            ProjectId = project.Id,
                            UserId = userId,
                            ActionType = "NameChanged",
                            Description = $"Project name changed from '{project.Name}' to '{request.Name}' (Approved by Admin)",
                            OldValue = project.Name,
                            NewValue = request.Name,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (project.Description != request.Description)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            ProjectId = project.Id,
                            UserId = userId,
                            ActionType = "DescriptionChanged",
                            Description = "Project description was updated (Approved by Admin)",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (project.StartDate != request.StartDate)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            ProjectId = project.Id,
                            UserId = userId,
                            ActionType = "StartDateChanged",
                            Description = $"Project start date changed to {request.StartDate:MMM dd, yyyy} (Approved by Admin)",
                            OldValue = project.StartDate.ToString("MMM dd, yyyy"),
                            NewValue = request.StartDate.ToString("MMM dd, yyyy"),
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (project.EndDate != request.EndDate)
                    {
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            ProjectId = project.Id,
                            UserId = userId,
                            ActionType = "EndDateChanged",
                            Description = $"Project end date changed to {request.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing"} (Approved by Admin)",
                            OldValue = project.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing",
                            NewValue = request.EndDate?.ToString("MMM dd, yyyy") ?? "Ongoing",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    project.Name = request.Name;
                    project.Description = request.Description;
                    project.StartDate = request.StartDate;
                    project.EndDate = request.EndDate;
                    project.Status = request.Status;
                    project.Issues = request.Issues;
                    project.UpdatedAt = DateTime.UtcNow;

                    var oldOwnerIds = project.Owners.Select(o => o.UserId).ToList();
                    var newOwnerIds = string.IsNullOrEmpty(request.OwnerIds)
                        ? new List<int>()
                        : request.OwnerIds.Split(',')
                            .Select(s => int.TryParse(s, out var parsedId) ? parsedId : 0)
                            .Where(x => x > 0)
                            .Distinct()
                            .ToList();

                    if (!oldOwnerIds.SequenceEqual(newOwnerIds))
                    {
                        var addedOwners = newOwnerIds.Except(oldOwnerIds).ToList();
                        var removedOwners = oldOwnerIds.Except(newOwnerIds).ToList();

                        _context.ProjectOwners.RemoveRange(project.Owners);
                        foreach (var ownerId in newOwnerIds)
                        {
                            project.Owners.Add(new ProjectOwner
                            {
                                ProjectId = project.Id,
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
                                _context.ActivityLogs.Add(new ActivityLog
                                {
                                    ProjectId = project.Id,
                                    UserId = userId,
                                    ActionType = "OwnerAdded",
                                    Description = $"Owners added: {string.Join(", ", addedNames)} (Approved by Admin)",
                                    CreatedAt = DateTime.UtcNow
                                });
                            }

                            if (removedOwners.Any())
                            {
                                var removedNames = ownerNames.Where(u => removedOwners.Contains(u.Id)).Select(u => $"{u.FirstName} {u.LastName}").ToList();
                                _context.ActivityLogs.Add(new ActivityLog
                                {
                                    ProjectId = project.Id,
                                    UserId = userId,
                                    ActionType = "OwnerRemoved",
                                    Description = $"Owners removed: {string.Join(", ", removedNames)} (Approved by Admin)",
                                    CreatedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }

                    _context.Projects.Update(project);

                    request.ApprovalStatus = "Approved";
                    request.ApprovedByUserId = userId;
                    request.ApprovedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Request to {request.RequestType.ToLower()} project '{request.Name}' approved successfully.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"An error occurred while approving the request: {ex.Message}";
            }
        }

        return RedirectToAction(nameof(PendingRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id, string rejectionReason)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin" && userRole != "Editor")
            return Forbid();

        var userId = HttpContext.Session.GetInt32("UserId") ?? 1;

        var request = await _context.ProjectApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        if (request.ApprovalStatus != "Pending")
        {
            TempData["ErrorMessage"] = "This request has already been processed.";
            return RedirectToAction(nameof(PendingRequests));
        }

        request.ApprovalStatus = "Rejected";
        request.ApprovedByUserId = userId;
        request.ApprovedAt = DateTime.UtcNow;
        request.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "No reason provided" : rejectionReason;

        _context.ProjectApprovalRequests.Update(request);

        _context.ActivityLogs.Add(new ActivityLog
        {
            ProjectId = request.ProjectId,
            UserId = userId,
            ActionType = "Rejected",
            Description = $"Request to {request.RequestType.ToLower()} project '{request.Name}' was rejected: {request.RejectionReason}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Request to {request.RequestType.ToLower()} project '{request.Name}' rejected successfully.";

        return RedirectToAction(nameof(PendingRequests));
    }
}
