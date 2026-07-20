namespace WebApplication1.Models;

public class ChatRoom
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsGroup { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public ICollection<ChatRoomMessage> Messages { get; set; } = new List<ChatRoomMessage>();
}
