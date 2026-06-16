using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class EventAttendee
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public int UserId { get; set; }

    [StringLength(50)]
    public string? AttendanceStatus { get; set; } // Attending, NotAttending, Maybe

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public OrganizationEvent? Event { get; set; }
    public User? User { get; set; }
}
