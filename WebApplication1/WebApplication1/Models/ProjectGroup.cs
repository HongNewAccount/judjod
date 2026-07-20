using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class ProjectGroup
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    public string? Color { get; set; } // hex color for visual distinction

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectGroupAssignment> ProjectAssignments { get; set; } = new List<ProjectGroupAssignment>();
}
