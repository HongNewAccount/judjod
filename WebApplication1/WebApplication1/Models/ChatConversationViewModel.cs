namespace WebApplication1.Models;

public class ChatConversationViewModel
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string? ProfileImagePath { get; set; }
    public string LastMessage { get; set; } = "";
    public DateTime LastTime { get; set; }
    public int UnreadCount { get; set; }
}
