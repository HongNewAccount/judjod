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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var ownedProjects = await _context.Projects
            .Where(p => p.Owners.Any(o => o.UserId == id))
            .Include(p => p.Group)
            .OrderBy(p => p.Name)
            .ToListAsync();

        ViewBag.OwnedProjects = ownedProjects;
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

        if (userRole != "Admin" && currentUserId != id)
        {
            return Forbid();
        }

        ModelState.Remove(nameof(WebApplication1.Models.User.Username));
        ModelState.Remove(nameof(WebApplication1.Models.User.PasswordHash));
        ModelState.Remove(nameof(WebApplication1.Models.User.LastName));

        if (ModelState.IsValid)
        {
            try
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser != null)
                {
                    existingUser.FirstName = user.FirstName?.Trim() ?? existingUser.FirstName;
                    existingUser.LastName = user.LastName?.Trim() ?? "";
                    existingUser.Email = user.Email?.Trim();
                    existingUser.Phone = user.Phone?.Trim();
                    existingUser.WorkLocation = user.WorkLocation?.Trim();
                    existingUser.UpdatedAt = DateTime.UtcNow;

                    if (profileImage != null && profileImage.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = $"{id}_{Guid.NewGuid()}{Path.GetExtension(profileImage.FileName)}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using var fileStream = new FileStream(filePath, FileMode.Create);
                        await profileImage.CopyToAsync(fileStream);

                        existingUser.ProfileImagePath = $"/uploads/profiles/{uniqueFileName}";
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

            return RedirectToAction(nameof(Details), new { id });
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBan(int id)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();

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
    public async Task<IActionResult> SetRole(int id, string role)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == id)
        {
            TempData["ErrorMessage"] = "You cannot change your own role.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (role != "Editor" && role != "User") return BadRequest();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Role = role;
        await _context.SaveChangesAsync();
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
