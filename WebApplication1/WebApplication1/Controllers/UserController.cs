using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using BCrypt.Net;

namespace WebApplication1.Controllers;

public class UserController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        return View(users);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users
            .Include(u => u.ReportsCreated)
            .Include(u => u.Assignments)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        var projectsOwnedCount = await _context.ProjectOwners.CountAsync(po => po.UserId == id);
        var projectsCreatedCount = await _context.Projects.CountAsync(p => p.CreatedByUserId == id);
        var eventsAttendedCount = await _context.EventAttendees.CountAsync(ea => ea.UserId == id);

        var recentActivity = await _context.ActivityLogs
            .Where(al => al.UserId == id)
            .Include(al => al.Project)
            .OrderByDescending(al => al.CreatedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.ProjectsOwnedCount = projectsOwnedCount;
        ViewBag.ProjectsCreatedCount = projectsCreatedCount;
        ViewBag.EventsAttendedCount = eventsAttendedCount;
        ViewBag.RecentActivity = recentActivity;

        return View(user);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(User user)
    {
        if (ModelState.IsValid)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            _context.Add(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(user);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        // Allow admin to edit anyone, or user to edit own profile
        if (userRole != "Admin" && currentUserId != id)
        {
            TempData["ErrorMessage"] = "You can only edit your own profile";
            return RedirectToAction(nameof(Index));
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, User user, IFormFile? profileImage)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");

        if (id != user.Id)
            return NotFound();

        // Allow admin to edit anyone, or user to edit own profile
        if (userRole != "Admin" && currentUserId != id)
        {
            return Forbid();
        }

        // This form never submits Username (not editable here) and PasswordHash is optional
        // (only changed if filled in), so neither should block validation.
        ModelState.Remove(nameof(WebApplication1.Models.User.Username));
        ModelState.Remove(nameof(WebApplication1.Models.User.PasswordHash));

        if (ModelState.IsValid)
        {
            try
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser != null)
                {
                    existingUser.FirstName = user.FirstName;
                    existingUser.LastName = user.LastName;
                    existingUser.NickName = user.NickName;
                    existingUser.Email = user.Email;
                    existingUser.Phone = user.Phone;
                    existingUser.Position = user.Position;
                    existingUser.WorkLocation = user.WorkLocation;
                    existingUser.Status = user.Status;
                    existingUser.Links = user.Links;

                    // Only admin can edit Role
                    if (userRole == "Admin")
                    {
                        existingUser.Role = user.Role;
                    }

                    // Handle profile image upload
                    if (profileImage != null && profileImage.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = $"{id}_{Guid.NewGuid()}_{Path.GetFileName(profileImage.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await profileImage.CopyToAsync(fileStream);
                        }

                        existingUser.ProfileImagePath = $"/uploads/profiles/{uniqueFileName}";
                    }

                    existingUser.UpdatedAt = DateTime.UtcNow;

                    if (!string.IsNullOrEmpty(user.PasswordHash))
                    {
                        existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                    }

                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Profile updated successfully.";
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBan(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = user.IsActive
            ? $"{user.FirstName} {user.LastName} has been unbanned."
            : $"{user.FirstName} {user.LastName} has been banned.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProjectSuspension(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.ProjectAccessSuspended = !user.ProjectAccessSuspended;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = user.ProjectAccessSuspended
            ? $"{user.FirstName} {user.LastName}'s project access has been suspended."
            : $"{user.FirstName} {user.LastName}'s project access has been restored.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
