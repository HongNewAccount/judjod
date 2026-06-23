using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class ProjectApprovalRequest
{
    public int Id { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    [Required]
    [StringLength(50)]
    public string RequestType { get; set; } = "Create"; // Create, Update

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [StringLength(50)]
    public string? Status { get; set; } // Planning, InProgress, OnHold, Completed, Closed

    [StringLength(500)]
    public string? Issues { get; set; }

    [StringLength(200)]
    public string? OwnerIds { get; set; } // Comma-separated list of UserIds, e.g. "1,3"

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }
}
