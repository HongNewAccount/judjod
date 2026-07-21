using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [Required]
    [StringLength(100)]
    public string Username { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    [Phone]
    public string? Phone { get; set; }

    [StringLength(50)]
    public string? Role { get; set; }

    [StringLength(200)]
    public string? WorkLocation { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [StringLength(500)]
    public string? ProfileImagePath { get; set; }

    public bool ProjectAccessSuspended { get; set; } = false;

    public bool ChatEnabled { get; set; } = true;
}
