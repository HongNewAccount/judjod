namespace WebApplication1.Models;

public class ProjectGroupAssignment
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int GroupId { get; set; }
    public Project Project { get; set; } = null!;
    public ProjectGroup Group { get; set; } = null!;
}
