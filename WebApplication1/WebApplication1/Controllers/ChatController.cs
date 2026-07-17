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

        var adminId = HttpContext.Session.GetInt32("UserId");
        var adminUser = adminId.HasValue ? await _context.Users.FindAsync(adminId.Value) : null;

        ViewBag.ChatUser = user;
        ViewBag.AdminUser = adminUser;
        ViewBag.ReturnUrl = from == "user"
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

        return Ok(new { id = msg.Id, content = msg.Content, isFromAdmin = msg.IsFromAdmin, createdAt = msg.CreatedAt.ToString("HH:mm") });
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

        return Ok(msgs.Select(m => new { id = m.Id, content = m.Content, isFromAdmin = m.IsFromAdmin, createdAt = m.CreatedAt.ToString("HH:mm") }));
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
