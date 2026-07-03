using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; }

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(50)]
    public string? Status { get; set; } // Planning, InProgress, OnHold, Completed, Closed

    [StringLength(500)]
    public string? Issues { get; set; } // JSON array or comma-separated

    public int? GroupId { get; set; }

    [StringLength(20)]
    public string? Priority { get; set; } // None, Low, Medium, High

    public int SortOrder { get; set; } = 0;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ProjectGroup? Group { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<ProjectOwner> Owners { get; set; } = new List<ProjectOwner>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
