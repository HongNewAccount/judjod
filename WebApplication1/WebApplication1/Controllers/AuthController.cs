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
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Invalid username or password");
            return View();
        }

        HttpContext.Session.SetInt32("UserId", user.Id);
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
        HttpContext.Session.SetString("UserRole", user.Role ?? "User");

        return RedirectToAction("Index", "ProjectTracker");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Register()
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            ViewBag.ErrorMessage = "Only Admin can register new users";
            return View();
        }

        var users = _context.Users.ToList();
        ViewBag.Users = users;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(User user)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin")
        {
            return RedirectToAction(nameof(Login));
        }

        if (await _context.Users.AnyAsync(u => u.Username == user.Username))
        {
            ModelState.AddModelError("Username", "Username already exists");
            var users = _context.Users.ToList();
            ViewBag.Users = users;
            return View(user);
        }

        if (ModelState.IsValid)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;
            user.Status = "Active";
            _context.Add(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User '{user.Username}' has been registered successfully.";
            return RedirectToAction(nameof(Register));
        }

        var usersList = _context.Users.ToList();
        ViewBag.Users = usersList;
        return View(user);
    }
}
