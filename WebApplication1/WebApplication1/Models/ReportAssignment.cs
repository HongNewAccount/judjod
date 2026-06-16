using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class ReportAssignment
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public int AssignedToUserId { get; set; }

    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedDate { get; set; }

    [StringLength(50)]
    public string? Status { get; set; } // Assigned, InProgress, Completed

    // Navigation properties
    public Report? Report { get; set; }
    public User? AssignedToUser { get; set; }
}
