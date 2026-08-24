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
            var msg = user.PendingApproval
                ? "บัญชีของคุณกำลังรอการอนุมัติจาก admin"
                : "This account has been banned. Please contact an administrator.";
            ModelState.AddModelError("", msg);
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

    // GET: shown to public (not logged in) for self-registration
    // Admin goes through User/Index modal instead
    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.GetInt32("UserId") != null)
            return RedirectToAction("Index", "ProjectTracker");
        return View();
    }

    // POST: admin creates user (AJAX from User/Index modal)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("Register")]
    public async Task<IActionResult> RegisterPost(string username, string password, string confirmPassword,
        string? firstName = null, string? lastName = null,
        bool chatEnabled = true, bool projectAccess = true)
    {
        var isAjax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        var isLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

        // Admin path: create user immediately (existing behavior)
        if (isSuperAdmin)
        {
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
                FirstName = string.IsNullOrWhiteSpace(firstName) ? username.Trim() : firstName.Trim(),
                LastName = lastName?.Trim() ?? "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = projectAccess ? "Editor" : "User",
                IsActive = true,
                PendingApproval = false,
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

        // Public registration path (not logged in)
        if (!isLoggedIn)
        {
            string? error = null;
            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                error = "Username ต้องมีอย่างน้อย 3 ตัวอักษร";
            else if (string.IsNullOrWhiteSpace(firstName))
                error = "กรุณากรอกชื่อ";
            else if (string.IsNullOrWhiteSpace(password))
                error = "กรุณากรอก Password";
            else if (password != confirmPassword)
                error = "Password ไม่ตรงกัน";
            else if (await _context.Users.AnyAsync(u => u.Username == username))
                error = $"Username '{username}' ถูกใช้งานแล้ว";

            if (error != null)
            {
                ViewBag.Error = error;
                ViewBag.Username = username;
                ViewBag.FirstName = firstName;
                ViewBag.LastName = lastName;
                return View();
            }

            var newUser = new User
            {
                Username = username.Trim(),
                FirstName = firstName!.Trim(),
                LastName = lastName?.Trim() ?? "",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "User",
                IsActive = false,
                PendingApproval = true,
                ChatEnabled = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "ลงทะเบียนสำเร็จ! กรุณารอ admin อนุมัติบัญชีของคุณ";
            return RedirectToAction(nameof(Login));
        }

        return Forbid();
    }
}
