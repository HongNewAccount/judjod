using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class OrganizationEvent
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    public DateTime EventDate { get; set; }

    public DateTime? EventEndDate { get; set; }

    [Required]
    [StringLength(500)]
    public string Location { get; set; }

    public bool IsOnSite { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? CreatedByUser { get; set; }
    public ICollection<EventAttendee> Attendees { get; set; } = new List<EventAttendee>();
}
