namespace WebApplication1.Models;

public class ChatRoomMessage
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = "";
    public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ChatRoom Room { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
