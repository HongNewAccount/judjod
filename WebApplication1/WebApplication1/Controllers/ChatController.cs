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
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Auth");

        var me = await _context.Users.FindAsync(userId.Value);
        if (me?.ChatEnabled == false)
        {
            TempData["ErrorMessage"] = "Chat access is disabled for your account.";
            return RedirectToAction("Index", "Dashboard");
        }

        await LoadRoomsViewBag(userId.Value);
        ViewBag.SelectedRoom = null;
        return View("Index");
    }

    public async Task<IActionResult> Room(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Auth");

        var room = await _context.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null) return NotFound();
        if (!room.Members.Any(m => m.UserId == userId)) return Forbid();

        var messages = await _context.ChatRoomMessages
            .Include(m => m.Sender)
            .Where(m => m.RoomId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var member = room.Members.First(m => m.UserId == userId);
        if (messages.Any() && member.LastReadMessageId < messages.Last().Id)
        {
            member.LastReadMessageId = messages.Last().Id;
            await _context.SaveChangesAsync();
        }

        var isAdmin = HttpContext.Session.GetString("UserRole") == "Admin";
        var isCreator = room.CreatedByUserId == userId;
        var canManage = isAdmin || isCreator;

        var memberUserIds = room.Members.Select(m => m.UserId).ToList();
        var nonMembers = room.IsGroup
            ? await _context.Users
                .Where(u => !memberUserIds.Contains(u.Id))
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync()
            : new List<User>();

        await LoadRoomsViewBag(userId.Value);

        ViewBag.SelectedRoom = room;
        ViewBag.Messages = messages;
        ViewBag.IsAdmin = isAdmin;
        ViewBag.CanManageGroup = canManage;
        ViewBag.NonMembers = nonMembers;
        ViewBag.CurrentUser = await _context.Users.FindAsync(userId.Value);

        return View("Index");
    }

    private async Task LoadRoomsViewBag(int userId)
    {
        var rooms = await _context.ChatRooms
            .Include(r => r.Members).ThenInclude(m => m.User)
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .ToListAsync();

        var roomIds = rooms.Select(r => r.Id).ToList();

        var lastMessages = await _context.ChatRoomMessages
            .Where(m => roomIds.Contains(m.RoomId))
            .GroupBy(m => m.RoomId)
            .Select(g => g.OrderByDescending(m => m.Id).First())
            .ToListAsync();

        var memberInfo = await _context.ChatRoomMembers
            .Where(m => m.UserId == userId && roomIds.Contains(m.RoomId))
            .ToDictionaryAsync(m => m.RoomId, m => m.LastReadMessageId);

        var unreadCounts = new Dictionary<int, int>();
        foreach (var roomId in roomIds)
        {
            var lastReadId = memberInfo.GetValueOrDefault(roomId, 0);
            unreadCounts[roomId] = await _context.ChatRoomMessages.CountAsync(m =>
                m.RoomId == roomId && m.Id > lastReadId && m.SenderId != userId);
        }

        var lastMessageTimes = lastMessages.ToDictionary(m => m.RoomId, m => m.CreatedAt);
        rooms = rooms.OrderByDescending(r => lastMessageTimes.GetValueOrDefault(r.Id, r.CreatedAt)).ToList();

        ViewBag.Rooms = rooms;
        ViewBag.LastMessages = lastMessages.ToDictionary(m => m.RoomId);
        ViewBag.UnreadCounts = unreadCounts;
        ViewBag.CurrentUserId = userId;

        var allUsers = await _context.Users
            .Where(u => u.Id != userId)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .ToListAsync();
        ViewBag.AllUsers = allUsers;
    }

    public async Task<IActionResult> StartDM(int userId)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == null) return RedirectToAction("Login", "Auth");
        if (userId == currentUserId) return RedirectToAction("Index");

        var existing = await _context.ChatRooms
            .Include(r => r.Members)
            .Where(r => !r.IsGroup
                && r.Members.Any(m => m.UserId == currentUserId)
                && r.Members.Any(m => m.UserId == userId))
            .FirstOrDefaultAsync(r => r.Members.Count == 2);

        if (existing != null)
            return RedirectToAction("Room", new { id = existing.Id });

        var room = new ChatRoom
        {
            IsGroup = false,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId.Value,
            Members = new List<ChatRoomMember>
            {
                new ChatRoomMember { UserId = currentUserId.Value, JoinedAt = DateTime.UtcNow },
                new ChatRoomMember { UserId = userId, JoinedAt = DateTime.UtcNow }
            }
        };
        _context.ChatRooms.Add(room);
        await _context.SaveChangesAsync();

        return RedirectToAction("Room", new { id = room.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(string name, List<int> memberIds)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == null) return RedirectToAction("Login", "Auth");

        if (string.IsNullOrWhiteSpace(name)) name = "Group Chat";

        var members = new List<ChatRoomMember>
        {
            new ChatRoomMember { UserId = currentUserId.Value, JoinedAt = DateTime.UtcNow }
        };
        foreach (var uid in memberIds.Where(uid => uid != currentUserId))
            members.Add(new ChatRoomMember { UserId = uid, JoinedAt = DateTime.UtcNow });

        var room = new ChatRoom
        {
            Name = name.Trim(),
            IsGroup = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId.Value,
            Members = members
        };
        _context.ChatRooms.Add(room);
        await _context.SaveChangesAsync();

        return RedirectToAction("Room", new { id = room.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int roomId, string content)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(content)) return BadRequest();

        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member == null) return Forbid();

        var msg = new ChatRoomMessage
        {
            RoomId = roomId,
            SenderId = userId.Value,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatRoomMessages.Add(msg);
        await _context.SaveChangesAsync();

        member.LastReadMessageId = msg.Id;
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId.Value);
        return Ok(new {
            id = msg.Id,
            content = msg.Content,
            senderId = msg.SenderId,
            senderName = $"{sender?.FirstName} {sender?.LastName}".Trim(),
            createdAt = msg.CreatedAt.ToString("HH:mm"),
            imagePath = (string?)null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendFile(int roomId, IFormFile file)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();
        if (file == null || file.Length == 0) return BadRequest();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext))
            return BadRequest("Only image files are allowed");

        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member == null) return Forbid();

        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(folder, fileName);
        using (var fs = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(fs);

        var imagePath = $"/uploads/chat/{fileName}";
        var msg = new ChatRoomMessage
        {
            RoomId = roomId,
            SenderId = userId.Value,
            Content = "",
            ImagePath = imagePath,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatRoomMessages.Add(msg);
        await _context.SaveChangesAsync();

        member.LastReadMessageId = msg.Id;
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId.Value);
        return Ok(new {
            id = msg.Id,
            content = msg.Content,
            senderId = msg.SenderId,
            senderName = $"{sender?.FirstName} {sender?.LastName}".Trim(),
            createdAt = msg.CreatedAt.ToString("HH:mm"),
            imagePath
        });
    }

    [HttpGet]
    public async Task<IActionResult> Poll(int roomId, int afterId)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();

        var msgs = await _context.ChatRoomMessages
            .Include(m => m.Sender)
            .Where(m => m.RoomId == roomId && m.Id > afterId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        if (msgs.Any())
        {
            var member = await _context.ChatRoomMembers
                .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
            if (member != null)
            {
                member.LastReadMessageId = msgs.Last().Id;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(msgs.Select(m => new {
            id = m.Id,
            content = m.Content,
            senderId = m.SenderId,
            senderName = $"{m.Sender?.FirstName} {m.Sender?.LastName}".Trim(),
            createdAt = m.CreatedAt.ToString("HH:mm"),
            imagePath = m.ImagePath,
            isSystem = m.IsSystemMessage
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int roomId, int userId)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var isAdmin = HttpContext.Session.GetString("UserRole") == "Admin";
        var room = await _context.ChatRooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null || !room.IsGroup) return NotFound();
        if (!isAdmin && room.CreatedByUserId != currentUserId) return Forbid();
        if (!room.Members.Any(m => m.UserId == userId))
        {
            _context.ChatRoomMembers.Add(new ChatRoomMember { RoomId = roomId, UserId = userId, JoinedAt = DateTime.UtcNow });
            var added = await _context.Users.FindAsync(userId);
            var actor = await _context.Users.FindAsync(currentUserId);
            _context.ChatRoomMessages.Add(new ChatRoomMessage
            {
                RoomId = roomId, SenderId = currentUserId!.Value, IsSystemMessage = true,
                Content = $"{added?.FirstName} {added?.LastName} was added by {actor?.FirstName}".Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Room", new { id = roomId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int roomId, int userId)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var isAdmin = HttpContext.Session.GetString("UserRole") == "Admin";
        var room = await _context.ChatRooms.FirstOrDefaultAsync(r => r.Id == roomId);
        if (room == null || !room.IsGroup) return NotFound();
        if (!isAdmin && room.CreatedByUserId != currentUserId) return Forbid();
        if (userId == room.CreatedByUserId) return BadRequest();
        var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member != null)
        {
            var removed = await _context.Users.FindAsync(userId);
            var actor   = await _context.Users.FindAsync(currentUserId);
            _context.ChatRoomMembers.Remove(member);
            _context.ChatRoomMessages.Add(new ChatRoomMessage
            {
                RoomId = roomId, SenderId = currentUserId!.Value, IsSystemMessage = true,
                Content = $"{removed?.FirstName} {removed?.LastName} was removed by {actor?.FirstName}".Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Room", new { id = roomId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LeaveRoom(int roomId)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", "Auth");

        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (member != null)
        {
            var user = await _context.Users.FindAsync(userId.Value);
            _context.ChatRoomMembers.Remove(member);
            _context.ChatRoomMessages.Add(new ChatRoomMessage
            {
                RoomId = roomId, SenderId = userId.Value, IsSystemMessage = true,
                Content = $"{user?.FirstName} {user?.LastName} left the group".Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var hasMembers = await _context.ChatRoomMembers.AnyAsync(m => m.RoomId == roomId);
            if (!hasMembers)
            {
                var room = await _context.ChatRooms.FindAsync(roomId);
                if (room != null) _context.ChatRooms.Remove(room);
                await _context.SaveChangesAsync();
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Unauthorized();

        var msg = await _context.ChatRoomMessages.FindAsync(id);
        if (msg == null) return NotFound();

        var isAdmin = HttpContext.Session.GetString("UserRole") == "Admin";
        if (msg.SenderId != userId && !isAdmin) return Forbid();

        _context.ChatRoomMessages.Remove(msg);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRoom(int roomId)
    {
        if (HttpContext.Session.GetString("UserRole") != "Admin") return Forbid();

        var messages = await _context.ChatRoomMessages.Where(m => m.RoomId == roomId).ToListAsync();
        _context.ChatRoomMessages.RemoveRange(messages);

        var members = await _context.ChatRoomMembers.Where(m => m.RoomId == roomId).ToListAsync();
        members.ForEach(m => m.LastReadMessageId = 0);

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Chat history cleared.";
        return RedirectToAction("Room", new { id = roomId });
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        if (HttpContext.Request.Headers["X-Requested-With"] != "XMLHttpRequest") return BadRequest();
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Ok(new { count = 0 });

        var memberships = await _context.ChatRoomMembers
            .Where(m => m.UserId == userId)
            .ToListAsync();

        var count = 0;
        foreach (var mem in memberships)
        {
            count += await _context.ChatRoomMessages.CountAsync(m =>
                m.RoomId == mem.RoomId &&
                m.Id > mem.LastReadMessageId &&
                m.SenderId != userId);
        }

        return Ok(new { count });
    }
}
