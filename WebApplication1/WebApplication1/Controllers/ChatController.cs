using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class ChatController : Controller
{
    private readonly ApplicationDbContext _context;
    public ChatController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var userId  = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Auth");
        var isAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";

        if (isAdmin)
        {
            var msgs = await _context.ChatMessages
                .Include(m => m.User)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var conversations = msgs
                .GroupBy(m => m.UserId)
                .Select(g => {
                    var last  = g.First();
                    var u     = last.User;
                    var fname = u?.FirstName ?? "";
                    var lname = u?.LastName  ?? "";
                    return new ChatConversationViewModel
                    {
                        UserId           = g.Key,
                        UserName         = $"{fname} {lname}".Trim(),
                        Initials         = $"{(fname.Length > 0 ? fname[0] : ' ')}{(lname.Length > 0 ? lname[0] : ' ')}".Trim(),
                        ProfileImagePath = u?.ProfileImagePath,
                        LastMessage      = last.Content,
                        LastTime         = last.CreatedAt,
                        UnreadCount      = g.Count(m => !m.IsFromAdmin && !m.IsRead)
                    };
                })
                .OrderByDescending(c => c.LastTime)
                .ToList();

            return View("AdminIndex", conversations);
        }
        else
        {
            var messages = await _context.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var unread = messages.Where(m => m.IsFromAdmin && !m.IsRead).ToList();
            unread.ForEach(m => m.IsRead = true);
            if (unread.Any()) await _context.SaveChangesAsync();

            var currentUser = await _context.Users.FindAsync(userId);
            var adminUser   = await _context.Users.FirstOrDefaultAsync(u => u.Role == "Admin");

            ViewBag.CurrentUser   = currentUser;
            ViewBag.AdminUser     = adminUser;
            ViewBag.CurrentUserId = userId;
            return View("UserChat", messages);
        }
    }

    public async Task<IActionResult> Conversation(int userId, string? from = null)
    {
        if (HttpContext.Session.GetString("IsSuperAdmin") != "true") return Forbid();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var messages = await _context.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var unread = messages.Where(m => !m.IsFromAdmin && !m.IsRead).ToList();
        unread.ForEach(m => m.IsRead = true);
        if (unread.Any()) await _context.SaveChangesAsync();

        var adminId   = HttpContext.Session.GetInt32("UserId");
        var adminUser = adminId.HasValue ? await _context.Users.FindAsync(adminId.Value) : null;

        var pendingRequests = await _context.ProjectApprovalRequests
            .Where(r => r.RequestedByUserId == userId && r.ApprovalStatus == "Pending" && r.RequestType == "RoleRequest")
            .OrderBy(r => r.RequestedAt)
            .ToListAsync();

        ViewBag.ChatUser       = user;
        ViewBag.AdminUser      = adminUser;
        ViewBag.PendingRequests = pendingRequests;
        ViewBag.ReturnUrl      = from == "user"
            ? Url.Action("Details", "User", new { id = userId })
            : Url.Action("Index", "Chat");
        return View("AdminChat", messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int userId, string content, bool isFromAdmin)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (string.IsNullOrWhiteSpace(content)) return BadRequest();

        var msg = new ChatMessage
        {
            UserId      = userId,
            IsFromAdmin = isFromAdmin,
            Content     = content.Trim(),
            CreatedAt   = DateTime.UtcNow
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        return Ok(new { id = msg.Id, content = msg.Content, isFromAdmin = msg.IsFromAdmin, createdAt = msg.CreatedAt.ToString("HH:mm"), imagePath = (string?)null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFile(int userId, bool isFromAdmin, IFormFile file)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (file == null || file.Length == 0) return BadRequest();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (!allowed.Contains(ext)) return BadRequest("Only image files are allowed");

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folder, fileName);
        using (var fs = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(fs);

        var imagePath = $"/uploads/chat/{fileName}";
        var msg = new ChatMessage
        {
            UserId      = userId,
            IsFromAdmin = isFromAdmin,
            Content     = "",
            ImagePath   = imagePath,
            CreatedAt   = DateTime.UtcNow
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        return Ok(new { id = msg.Id, content = msg.Content, isFromAdmin = msg.IsFromAdmin, createdAt = msg.CreatedAt.ToString("HH:mm"), imagePath });
    }

    [HttpGet]
    public async Task<IActionResult> Poll(int userId, int afterId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var isAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";

        var msgs = await _context.ChatMessages
            .Where(m => m.UserId == userId && m.Id > afterId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var unread = msgs.Where(m => m.IsFromAdmin != isAdmin && !m.IsRead).ToList();
        unread.ForEach(m => m.IsRead = true);
        if (unread.Any()) await _context.SaveChangesAsync();

        return Ok(msgs.Select(m => new {
            id          = m.Id,
            content     = m.Content,
            isFromAdmin = m.IsFromAdmin,
            createdAt   = m.CreatedAt.ToString("HH:mm"),
            imagePath   = m.ImagePath
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRoleChange()
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        if (user.Role == "Editor") return BadRequest("Already an Editor");

        var existing = await _context.ProjectApprovalRequests
            .AnyAsync(r => r.RequestedByUserId == userId.Value && r.ApprovalStatus == "Pending" && r.RequestType == "RoleRequest");
        if (existing) return Ok(new { alreadyPending = true });

        var req = new ProjectApprovalRequest
        {
            ProjectId         = null,
            RequestType       = "RoleRequest",
            Name              = "Role",
            Description       = "Editor",
            Issues            = $"{user.FirstName} {user.LastName}",
            StartDate         = DateTime.UtcNow,
            RequestedByUserId = userId.Value,
            ApprovalStatus    = "Pending",
            RequestedAt       = DateTime.UtcNow
        };
        _context.ProjectApprovalRequests.Add(req);

        var msg = new ChatMessage
        {
            UserId      = userId.Value,
            IsFromAdmin = false,
            Content     = "🔑 ขอสิทธิ์ Editor เพื่อแก้ไข Task",
            IsRead      = true,   // don't double-count with the PendingRoleRequest badge
            CreatedAt   = DateTime.UtcNow
        };
        _context.ChatMessages.Add(msg);
        await _context.SaveChangesAsync();

        return Ok(new { id = msg.Id, content = msg.Content, isFromAdmin = false, createdAt = msg.CreatedAt.ToString("HH:mm"), imagePath = (string?)null });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRoleRequest(int requestId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (HttpContext.Session.GetString("IsSuperAdmin") != "true") return Forbid();

        var adminId = HttpContext.Session.GetInt32("UserId");
        var req = await _context.ProjectApprovalRequests.FindAsync(requestId);
        if (req == null) return NotFound();

        req.ApprovalStatus   = "Approved";
        req.ApprovedByUserId = adminId;
        req.ApprovedAt       = DateTime.UtcNow;

        var targetUser = await _context.Users.FindAsync(req.RequestedByUserId);
        if (targetUser != null)
        {
            targetUser.Role = "Editor";
            _context.ActivityLogs.Add(new ActivityLog
            {
                UserId      = adminId,
                ActionType  = "RoleChanged",
                Description = $"{targetUser.FirstName} {targetUser.LastName} promoted to Editor via chat request",
                OldValue    = "User",
                NewValue    = "Editor",
                CreatedAt   = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRoleRequest(int requestId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (HttpContext.Session.GetString("IsSuperAdmin") != "true") return Forbid();

        var req = await _context.ProjectApprovalRequests.FindAsync(requestId);
        if (req == null) return NotFound();

        req.ApprovalStatus = "Rejected";
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> PendingRoleRequests(int userId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        if (HttpContext.Session.GetString("IsSuperAdmin") != "true") return Forbid();

        var reqs = await _context.ProjectApprovalRequests
            .Where(r => r.RequestedByUserId == userId && r.ApprovalStatus == "Pending" && r.RequestType == "RoleRequest")
            .OrderBy(r => r.RequestedAt)
            .Select(r => new { r.Id, displayName = r.Issues })
            .ToListAsync();

        return Ok(reqs);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId  = HttpContext.Session.GetInt32("UserId");
        var isAdmin = HttpContext.Session.GetString("IsSuperAdmin") == "true";

        int count = isAdmin
            ? await _context.ChatMessages.CountAsync(m => !m.IsFromAdmin && !m.IsRead)
            : await _context.ChatMessages.CountAsync(m => m.UserId == userId && m.IsFromAdmin && !m.IsRead);

        return Ok(new { count });
    }
}
