using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class ReportComment
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public int UserId { get; set; }

    [Required]
    public string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Report? Report { get; set; }
    public User? User { get; set; }
}
