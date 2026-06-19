namespace WebApplication1.Models;

public class ProjectFavorite
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Project? Project { get; set; }
    public User? User { get; set; }
}
