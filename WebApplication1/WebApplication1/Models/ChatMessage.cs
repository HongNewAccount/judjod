namespace WebApplication1.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }       // non-admin user in the conversation
    public bool IsFromAdmin { get; set; } // true = admin sent, false = user sent
    public string Content { get; set; } = "";
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
