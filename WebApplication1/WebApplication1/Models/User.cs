using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; }

    [Required]
    [StringLength(100)]
    public string LastName { get; set; }

    [StringLength(100)]
    public string? NickName { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Line { get; set; }

    [StringLength(50)]
    public string? Role { get; set; }

    [StringLength(100)]
    public string? Position { get; set; }

    [StringLength(200)]
    public string? WorkLocation { get; set; }

    [StringLength(20)]
    public string? Status { get; set; } // Available, Unavailable, Absent

    [StringLength(500)]
    public string? Links { get; set; } // JSON or comma-separated links

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Report> ReportsCreated { get; set; } = new List<Report>();
    public ICollection<ReportAssignment> Assignments { get; set; } = new List<ReportAssignment>();
    public ICollection<ReportComment> Comments { get; set; } = new List<ReportComment>();
    public ICollection<EventAttendee> EventAttendees { get; set; } = new List<EventAttendee>();
}
