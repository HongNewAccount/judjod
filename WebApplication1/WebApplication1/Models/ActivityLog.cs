namespace WebApplication1.Models;

public class ActivityLog
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? UserId { get; set; }
    public string ActionType { get; set; }
    public string Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
    public User? User { get; set; }
}
