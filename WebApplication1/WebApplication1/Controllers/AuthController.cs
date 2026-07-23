using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using BCrypt.Net;

namespace WebApplication1.Controllers;

public class AuthController : Controller
{
    private readonly ApplicationDbContext _context;

    public AuthController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user != null && !user.IsActive)
        {
            ModelState.AddModelError("", "This account has been banned. Please contact an administrator.");
            return View();
        }

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid username or password");
            return View();
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserRole", user.Role ?? "User");
        HttpContext.Session.SetString("IsSuperAdmin", user.Role == "Admin" ? "true" : "false");

        return RedirectToAction("Index", "ProjectTracker");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Register()
    {
        return RedirectToAction("Index", "User");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string username, string password, string confirmPassword, bool chatEnabled = true, bool projectAccess = true)
    {
        var isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin)
        {
            if (isAjax) return Json(new { success = false, error = "Unauthorized" });
            return RedirectToAction(nameof(Login));
        }

        string? error = null;
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            error = "Username must be at least 3 characters.";
        else if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            error = "Password must be at least 4 characters.";
        else if (password != confirmPassword)
            error = "Passwords do not match.";
        else if (await _context.Users.AnyAsync(u => u.Username == username))
            error = $"Username '{username}' already exists.";

        if (error != null)
        {
            if (isAjax) return Json(new { success = false, error });
            TempData["ErrorMessage"] = error;
            return RedirectToAction("Index", "User");
        }

        var user = new User
        {
            Username = username.Trim(),
            FirstName = username.Trim(),
            LastName = "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = projectAccess ? "Editor" : "User",
            IsActive = true,
            ChatEnabled = chatEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _context.Add(user);

        var creatorId = HttpContext.Session.GetInt32("UserId");
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = creatorId,
            ProjectId = null,
            ActionType = "UserCreated",
            Description = $"New user '{username}' created as {user.Role}",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        if (isAjax) return Json(new { success = true, message = $"User '{username}' created successfully." });
        TempData["SuccessMessage"] = $"User '{username}' created successfully.";
        return RedirectToAction("Index", "User");
    }
}
