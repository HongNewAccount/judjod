using WebApplication1.Models;
using BCrypt.Net;

namespace WebApplication1.Data;

public static class SeedData
{
    public static void Initialize(ApplicationDbContext context)
    {
        if (context.Users.Any())
        {
            return;
        }

        var users = new[]
        {
            new User
            {
                FirstName = "Admin",
                LastName = "User",
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Email = "admin@example.com",
                Phone = "0812345678",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "John",
                LastName = "Doe",
                Username = "john",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("john123"),
                Email = "john@example.com",
                Phone = "0812345679",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "Jane",
                LastName = "Smith",
                Username = "jane",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("jane123"),
                Email = "jane@example.com",
                Phone = "0812345680",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.Users.AddRange(users);
        context.SaveChanges();

        var projects = new[]
        {
            new Project
            {
                Name = "Website Redesign",
                Description = "Redesign the company website with modern UI/UX",
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(20),
                Status = "InProgress",
                Issues = "Need design approval from stakeholders",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow
            },
            new Project
            {
                Name = "Mobile App Development",
                Description = "Develop iOS and Android mobile applications",
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(30),
                Status = "InProgress",
                CreatedByUserId = users[1].Id,
                CreatedAt = DateTime.UtcNow
            },
            new Project
            {
                Name = "Database Migration",
                Description = "Migrate from SQL Server to MySQL",
                StartDate = DateTime.UtcNow.AddDays(-20),
                EndDate = DateTime.UtcNow.AddDays(5),
                Status = "Planning",
                Issues = "Backup strategy needs review",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.Projects.AddRange(projects);
        context.SaveChanges();

        var projectOwners = new[]
        {
            new ProjectOwner { ProjectId = projects[0].Id, UserId = users[1].Id },
            new ProjectOwner { ProjectId = projects[0].Id, UserId = users[2].Id },
            new ProjectOwner { ProjectId = projects[1].Id, UserId = users[1].Id },
            new ProjectOwner { ProjectId = projects[2].Id, UserId = users[0].Id }
        };

        context.ProjectOwners.AddRange(projectOwners);
        context.SaveChanges();

        var activityLogs = new[]
        {
            new ActivityLog
            {
                ProjectId = projects[0].Id,
                UserId = users[0].Id,
                ActionType = "Created",
                Description = "Project 'Website Redesign' was created",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new ActivityLog
            {
                ProjectId = projects[1].Id,
                UserId = users[1].Id,
                ActionType = "Created",
                Description = "Project 'Mobile App Development' was created",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        context.ActivityLogs.AddRange(activityLogs);
        context.SaveChanges();
    }
}
