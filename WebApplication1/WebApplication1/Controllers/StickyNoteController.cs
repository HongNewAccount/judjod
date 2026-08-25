using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class StickyNoteController : Controller
{
    private readonly ApplicationDbContext _context;
    private const int ExpiryDays = 30;
    private static readonly string[] ValidColors =
        ["#fef08a", "#fecdd3", "#bbf7d0", "#bae6fd", "#e9d5ff", "#fed7aa", "#f1f5f9"];

    public StickyNoteController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Auth");

        await _context.StickyNotes.Where(n => n.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync();

        var notes = await _context.StickyNotes
            .Include(n => n.User)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View(notes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string? title, string content, string color)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "กรุณากรอกข้อความ" });

        var note = new StickyNote
        {
            Title     = title?.Trim() ?? "",
            Content   = content.Trim(),
            Color     = ValidColors.Contains(color) ? color : "#fef08a",
            UserId    = userId.Value,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(ExpiryDays)
        };

        _context.StickyNotes.Add(note);
        _context.ActivityLogs.Add(new ActivityLog
        {
            ProjectId   = null,
            UserId      = userId.Value,
            ActionType  = "NoteCreated",
            Description = $"สร้าง Note '{(string.IsNullOrEmpty(note.Title) ? "(ไม่มีหัวข้อ)" : note.Title)}'",
            NewValue    = note.Content,
            CreatedAt   = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId.Value);
        return Ok(new
        {
            success       = true,
            id            = note.Id,
            title         = note.Title,
            content       = note.Content,
            color         = note.Color,
            authorName    = $"{user!.FirstName} {user.LastName}".Trim(),
            authorInitial = user.FirstName[0].ToString().ToUpper(),
            daysLeft      = ExpiryDays
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string? title, string content, string color)
    {
        var userId   = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userId == null) return Forbid();

        var note = await _context.StickyNotes.FindAsync(id);
        if (note == null) return NotFound();
        if (note.UserId != userId && userRole != "Admin") return Forbid();

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "กรุณากรอกข้อความ" });

        var oldTitle   = note.Title;
        var oldContent = note.Content;

        note.Title   = title?.Trim() ?? "";
        note.Content = content.Trim();
        note.Color   = ValidColors.Contains(color) ? color : note.Color;

        _context.ActivityLogs.Add(new ActivityLog
        {
            ProjectId   = null,
            UserId      = userId.Value,
            ActionType  = "NoteEdited",
            Description = $"แก้ไข Note '{(string.IsNullOrEmpty(oldTitle) ? "(ไม่มีหัวข้อ)" : oldTitle)}'",
            OldValue    = oldContent,
            NewValue    = note.Content,
            CreatedAt   = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            id      = note.Id,
            title   = note.Title,
            content = note.Content,
            color   = note.Color
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId   = HttpContext.Session.GetInt32("UserId");
        var userRole = HttpContext.Session.GetString("UserRole");
        if (userId == null) return Forbid();

        var note = await _context.StickyNotes.FindAsync(id);
        if (note == null) return NotFound();
        if (note.UserId != userId && userRole != "Admin" && userRole != "Editor") return Forbid();

        var noteTitle   = note.Title;
        var noteContent = note.Content;

        _context.StickyNotes.Remove(note);
        _context.ActivityLogs.Add(new ActivityLog
        {
            ProjectId   = null,
            UserId      = userId.Value,
            ActionType  = "NoteDeleted",
            Description = $"ลบ Note '{(string.IsNullOrEmpty(noteTitle) ? "(ไม่มีหัวข้อ)" : noteTitle)}'",
            OldValue    = noteContent,
            CreatedAt   = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }
}
