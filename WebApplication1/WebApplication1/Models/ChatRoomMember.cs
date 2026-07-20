namespace WebApplication1.Models;

public class ChatRoomMember
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public int LastReadMessageId { get; set; } = 0;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public ChatRoom Room { get; set; } = null!;
    public User User { get; set; } = null!;
}
