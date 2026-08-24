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
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";

        var users = await _context.Users
            .Where(u => !u.PendingApproval)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        if (isSuperAdmin)
        {
            var pending = await _context.Users
                .Where(u => u.PendingApproval)
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();
            ViewBag.PendingUsers = pending;
        }

        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRegistration(int id)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null || !user.PendingApproval) return NotFound();

        user.IsActive = true;
        user.PendingApproval = false;

        var adminId = HttpContext.Session.GetInt32("UserId");
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = adminId,
            ActionType = "UserApproved",
            Description = $"อนุมัติการลงทะเบียนของ '{user.Username}' ({user.FirstName} {user.LastName})",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { success = true });

        TempData["SuccessMessage"] = $"อนุมัติ '{user.FirstName} {user.LastName}' สำเร็จ";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRegistration(int id)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null || !user.PendingApproval) return NotFound();

        var adminId = HttpContext.Session.GetInt32("UserId");
        var uname = user.Username;
        var fname = $"{user.FirstName} {user.LastName}".Trim();

        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId = adminId,
            ActionType = "UserRejected",
            Description = $"ปฏิเสธการลงทะเบียนของ '{uname}' ({fname})",
            CreatedAt = DateTime.UtcNow
        });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { success = true });

        TempData["SuccessMessage"] = $"ปฏิเสธการลงทะเบียนของ '{uname}' แล้ว";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var ownedProjects = await _context.Projects
            .Where(p => p.Owners.Any(o => o.UserId == id))
            .Include(p => p.Groups)
                .ThenInclude(pg => pg.Group)
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
    public async Task<IActionResult> Edit(int id, User user, IFormFile? profileImage,
        string? currentPassword = null, string? newPassword = null, string? confirmPassword = null)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");

        if (id != user.Id)
            return NotFound();

        if (userRole != "Admin" && currentUserId != id)
            return Forbid();

        var existingUser = await _context.Users.FindAsync(id);
        if (existingUser == null) return NotFound();

        bool isAdmin = userRole == "Admin";
        bool isSelf  = currentUserId == id;

        // Validate password change if requested
        bool changingPassword = !string.IsNullOrWhiteSpace(newPassword);
        if (changingPassword)
        {
            if (isSelf && !isAdmin)
            {
                if (string.IsNullOrWhiteSpace(currentPassword) ||
                    !BCrypt.Net.BCrypt.Verify(currentPassword, existingUser.PasswordHash))
                {
                    ViewBag.PasswordError = "รหัสผ่านปัจจุบันไม่ถูกต้อง";
                    return View(existingUser);
                }
            }
            if (newPassword != confirmPassword)
            {
                ViewBag.PasswordError = "รหัสผ่านใหม่ไม่ตรงกัน";
                return View(existingUser);
            }
        }

        ModelState.Remove(nameof(WebApplication1.Models.User.Username));
        ModelState.Remove(nameof(WebApplication1.Models.User.PasswordHash));
        ModelState.Remove(nameof(WebApplication1.Models.User.LastName));

        if (ModelState.IsValid)
        {
            try
            {
                existingUser.FirstName = user.FirstName?.Trim() ?? existingUser.FirstName;
                existingUser.LastName = user.LastName?.Trim() ?? "";
                existingUser.Email = user.Email?.Trim();
                existingUser.Phone = user.Phone?.Trim();
                existingUser.WorkLocation = user.WorkLocation?.Trim();
                existingUser.BirthDate = user.BirthDate;
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

                if (changingPassword)
                {
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword!);
                    _context.ActivityLogs.Add(new ActivityLog
                    {
                        UserId = currentUserId,
                        ActionType = "PasswordChanged",
                        Description = isSelf
                            ? $"{existingUser.FirstName} {existingUser.LastName} เปลี่ยนรหัสผ่านของตัวเอง"
                            : $"Admin เปลี่ยนรหัสผ่านให้ {existingUser.FirstName} {existingUser.LastName}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.Update(existingUser);
                _context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = currentUserId,
                    ProjectId = null,
                    ActionType = "ProfileUpdated",
                    Description = $"{existingUser.FirstName} {existingUser.LastName}'s profile was updated",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = changingPassword ? "อัปเดตโปรไฟล์และเปลี่ยนรหัสผ่านสำเร็จ" : "Profile updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        return View(existingUser);
    }

    public async Task<IActionResult> ChangePassword(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (userRole != "Admin" && currentUserId != id) return Forbid();
        ViewBag.TargetUser = user;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(int id, string? currentPassword, string newPassword, string confirmPassword)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        bool isAdmin = userRole == "Admin";
        bool isSelf  = currentUserId == id;
        if (!isAdmin && !isSelf) return Forbid();

        // Non-admin changing own password must verify current password
        if (isSelf && !isAdmin)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) ||
                !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ViewBag.ErrorMessage = "รหัสผ่านปัจจุบันไม่ถูกต้อง";
                ViewBag.TargetUser = user;
                return View();
            }
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ViewBag.ErrorMessage = "กรุณากรอกรหัสผ่านใหม่";
            ViewBag.TargetUser = user;
            return View();
        }
        if (newPassword != confirmPassword)
        {
            ViewBag.ErrorMessage = "รหัสผ่านไม่ตรงกัน";
            ViewBag.TargetUser = user;
            return View();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _context.ActivityLogs.Add(new ActivityLog
        {
            UserId      = currentUserId,
            ActionType  = "PasswordChanged",
            Description = isSelf
                ? $"{user.FirstName} {user.LastName} เปลี่ยนรหัสผ่านของตัวเอง"
                : $"Admin เปลี่ยนรหัสผ่านให้ {user.FirstName} {user.LastName}",
            CreatedAt   = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "เปลี่ยนรหัสผ่านสำเร็จ";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleBan(int id)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { value = user.IsActive });

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
        if (userRole != "Admin") return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.ProjectAccessSuspended = !user.ProjectAccessSuspended;
        await _context.SaveChangesAsync();

        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { value = !user.ProjectAccessSuspended });

        TempData["SuccessMessage"] = user.ProjectAccessSuspended
            ? $"{user.FirstName} {user.LastName}'s project access has been suspended."
            : $"{user.FirstName} {user.LastName}'s project access has been restored.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRole(int id)
    {
        var isSuperAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";
        if (!isSuperAdmin) return Forbid();
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == id) return BadRequest();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Role = user.Role == "Editor" ? "User" : "Editor";
        await _context.SaveChangesAsync();

        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { value = user.Role == "Editor" });

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleChatEnabled(int id)
    {
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userRole != "Admin") return Forbid();

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.ChatEnabled = !user.ChatEnabled;
        await _context.SaveChangesAsync();

        if (HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { value = user.ChatEnabled });

        return RedirectToAction(nameof(Details), new { id });
    }
}
