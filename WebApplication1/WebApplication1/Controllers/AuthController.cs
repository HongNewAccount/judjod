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
        // SuperAdmin = first user (ID=1), has full control (ban, toggle edit)
        HttpContext.Session.SetString("IsSuperAdmin", user.Id == 1 ? "true" : "false");

        return RedirectToAction("Index", "ProjectTracker");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Register()
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin)
        {
            return RedirectToAction("Index", "User");
        }

        var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
        ViewBag.Users = users;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string username, string password, string confirmPassword, bool canEdit = false)
    {
        var isSuperAdmin2 = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin2)
        {
            return RedirectToAction(nameof(Login));
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            ViewBag.ErrorMessage = "Username must be at least 3 characters.";
            return View();
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            ViewBag.ErrorMessage = "Password must be at least 4 characters.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.ErrorMessage = "Passwords do not match.";
            return View();
        }

        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            ViewBag.ErrorMessage = $"Username '{username}' already exists.";
            return View();
        }

        // Only super admin (ID=1) can grant edit access
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        var user = new User
        {
            Username = username.Trim(),
            FirstName = username.Trim(),
            LastName = "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = (canEdit && isSuperAdmin) ? "Admin" : "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Add(user);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"User '{username}' created successfully as {user.Role}.";
        return RedirectToAction(nameof(Register));
    }
}
