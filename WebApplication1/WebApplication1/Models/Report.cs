using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class Report
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    [StringLength(50)]
    public string? Priority { get; set; } // Low, Medium, High, Urgent

    [StringLength(50)]
    public string? Status { get; set; } // Pending, InProgress, Resolved, Closed

    public DateTime ReportedDate { get; set; } = DateTime.UtcNow;

    public DateTime? ScheduledDate { get; set; }

    public DateTime? ScheduledEndDate { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    public bool IsOnSite { get; set; } = true;

    [StringLength(500)]
    public string? ImageUrls { get; set; } // JSON array

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? CreatedByUser { get; set; }
    public ICollection<ReportAssignment> Assignments { get; set; } = new List<ReportAssignment>();
    public ICollection<ReportComment> Comments { get; set; } = new List<ReportComment>();
}
