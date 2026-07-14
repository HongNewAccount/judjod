namespace WebApplication1.Models;

public class ProjectProgressLog
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    public int Progress { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Project? Project { get; set; }
    public User? User { get; set; }
}
