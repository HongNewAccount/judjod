using WebApplication1.Models;
using BCryptNet = BCrypt.Net.BCrypt;

namespace WebApplication1.Data;

public static class SeedData
{
    public static void Initialize(ApplicationDbContext context)
    {
        // Only initialize if database is empty
        if (context.Users.Any())
        {
            return;
        }

        // Create test users
        var users = new List<User>
        {
            new User
            {
                FirstName = "Test",
                LastName = "User",
                NickName = "Test",
                Username = "test",
                PasswordHash = BCryptNet.HashPassword("1234"),
                Email = "test@organization.com",
                Phone = "081-000-0000",
                Line = "test_line",
                Role = "Admin",
                Position = "Administrator",
                WorkLocation = "Floor 1, Desk 1",
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "Admin",
                LastName = "User",
                NickName = "Admin",
                Username = "admin",
                PasswordHash = BCryptNet.HashPassword("admin123"),
                Email = "admin@organization.com",
                Phone = "081-000-0000",
                Line = "admin_line",
                Role = "Admin",
                Position = "Administrator",
                WorkLocation = "Floor 1, Desk 1",
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "Somchai",
                LastName = "Developer",
                NickName = "Som",
                Username = "somchai",
                PasswordHash = BCryptNet.HashPassword("pass123"),
                Email = "somchai@organization.com",
                Phone = "081-111-1111",
                Line = "som_dev",
                Role = "Manager",
                Position = "Project Manager",
                WorkLocation = "Floor 2, Desk 5",
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "Niran",
                LastName = "Engineer",
                NickName = "Niran",
                Username = "niran",
                PasswordHash = BCryptNet.HashPassword("pass123"),
                Email = "niran@organization.com",
                Phone = "081-222-2222",
                Line = "niran_eng",
                Role = "User",
                Position = "Software Engineer",
                WorkLocation = "Floor 2, Desk 10",
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                FirstName = "Wilai",
                LastName = "Designer",
                NickName = "Wilai",
                Username = "wilai",
                PasswordHash = BCryptNet.HashPassword("pass123"),
                Email = "wilai@organization.com",
                Phone = "081-333-3333",
                Line = "wilai_design",
                Role = "User",
                Position = "UI/UX Designer",
                WorkLocation = "Floor 3, Desk 2",
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        context.Users.AddRange(users);
        context.SaveChanges();

        // Create test projects
        var projects = new List<Project>
        {
            new Project
            {
                Name = "Website Redesign 2026",
                Description = "Complete redesign of company website with new UI/UX",
                StartDate = new DateTime(2026, 1, 15),
                EndDate = new DateTime(2026, 6, 30),
                Status = "InProgress",
                Issues = "Waiting for design approval from management",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new Project
            {
                Name = "Mobile App Development",
                Description = "Develop iOS and Android apps for customer portal",
                StartDate = new DateTime(2026, 2, 1),
                EndDate = new DateTime(2026, 8, 31),
                Status = "InProgress",
                Issues = "Need additional resources for testing phase",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },
            new Project
            {
                Name = "Database Migration",
                Description = "Migrate legacy database to cloud infrastructure",
                StartDate = new DateTime(2026, 3, 1),
                EndDate = new DateTime(2026, 5, 31),
                Status = "Planning",
                Issues = null,
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new Project
            {
                Name = "Security Audit",
                Description = "Full security assessment and penetration testing",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 4, 15),
                Status = "Completed",
                Issues = "Minor vulnerabilities found and patched",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            },
            new Project
            {
                Name = "API Integration",
                Description = "Integrate third-party payment and analytics APIs",
                StartDate = new DateTime(2026, 4, 1),
                EndDate = new DateTime(2026, 6, 15),
                Status = "Late",
                Issues = "Waiting for vendor API documentation update",
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new Project
            {
                Name = "Email Marketing System",
                Description = "Build automated email marketing platform",
                StartDate = new DateTime(2026, 5, 1),
                EndDate = new DateTime(2026, 7, 30),
                Status = "Planning",
                Issues = null,
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        context.Projects.AddRange(projects);
        context.SaveChanges();

        // Add project owners
        var projectOwners = new List<ProjectOwner>
        {
            // Website Redesign - Somchai and Wilai are owners
            new ProjectOwner { ProjectId = projects[0].Id, UserId = users[1].Id },
            new ProjectOwner { ProjectId = projects[0].Id, UserId = users[3].Id },

            // Mobile App Development - Somchai and Niran are owners
            new ProjectOwner { ProjectId = projects[1].Id, UserId = users[1].Id },
            new ProjectOwner { ProjectId = projects[1].Id, UserId = users[2].Id },

            // Database Migration - Niran is owner
            new ProjectOwner { ProjectId = projects[2].Id, UserId = users[2].Id },

            // Security Audit - Admin and Niran are owners
            new ProjectOwner { ProjectId = projects[3].Id, UserId = users[0].Id },
            new ProjectOwner { ProjectId = projects[3].Id, UserId = users[2].Id },

            // API Integration - Somchai is owner
            new ProjectOwner { ProjectId = projects[4].Id, UserId = users[1].Id },

            // Email Marketing System - Wilai is owner
            new ProjectOwner { ProjectId = projects[5].Id, UserId = users[3].Id }
        };

        context.ProjectOwners.AddRange(projectOwners);
        context.SaveChanges();

        // Add project favorites (mark some projects as favorite for users)
        var projectFavorites = new List<ProjectFavorite>
        {
            new ProjectFavorite { ProjectId = projects[0].Id, UserId = users[1].Id, CreatedAt = DateTime.UtcNow },
            new ProjectFavorite { ProjectId = projects[1].Id, UserId = users[2].Id, CreatedAt = DateTime.UtcNow },
            new ProjectFavorite { ProjectId = projects[3].Id, UserId = users[0].Id, CreatedAt = DateTime.UtcNow }
        };

        context.ProjectFavorites.AddRange(projectFavorites);
        context.SaveChanges();

        // Add activity logs for each project
        var activityLogs = new List<ActivityLog>();

        for (int i = 0; i < projects.Count; i++)
        {
            activityLogs.Add(new ActivityLog
            {
                ProjectId = projects[i].Id,
                UserId = projects[i].CreatedByUserId,
                ActionType = "Created",
                Description = $"Project '{projects[i].Name}' was created",
                CreatedAt = projects[i].CreatedAt
            });
        }

        // Add status change logs
        activityLogs.Add(new ActivityLog
        {
            ProjectId = projects[0].Id,
            UserId = users[0].Id,
            ActionType = "StatusChanged",
            Description = "Project status changed from 'Planning' to 'InProgress'",
            OldValue = "Planning",
            NewValue = "InProgress",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        });

        activityLogs.Add(new ActivityLog
        {
            ProjectId = projects[3].Id,
            UserId = users[0].Id,
            ActionType = "StatusChanged",
            Description = "Project status changed from 'InProgress' to 'Completed'",
            OldValue = "InProgress",
            NewValue = "Completed",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });

        context.ActivityLogs.AddRange(activityLogs);
        context.SaveChanges();

        // Create test reports
        var reports = new List<Report>
        {
            new Report
            {
                Title = "Network Connectivity Issue",
                Description = "Internet connection drops frequently on Floor 2. Network performance is affecting productivity.",
                Priority = "High",
                Status = "InProgress",
                ReportedDate = DateTime.UtcNow.AddDays(-5),
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                Location = "Floor 2, Conference Room",
                IsOnSite = true,
                CreatedByUserId = users[1].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Report
            {
                Title = "Computer Hardware Failure",
                Description = "Desktop computer in Dev Lab crashes randomly. May need hardware replacement.",
                Priority = "Medium",
                Status = "Pending",
                ReportedDate = DateTime.UtcNow.AddDays(-3),
                ScheduledDate = DateTime.UtcNow.AddDays(2),
                Location = "Floor 2, Dev Lab",
                IsOnSite = true,
                CreatedByUserId = users[2].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Report
            {
                Title = "Leave Request",
                Description = "Request for annual leave on June 20-24, 2026 for family vacation",
                Priority = "Low",
                Status = "Pending",
                ReportedDate = DateTime.UtcNow.AddDays(-2),
                ScheduledDate = new DateTime(2026, 6, 20),
                Location = "Out of office",
                IsOnSite = false,
                CreatedByUserId = users[3].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Report
            {
                Title = "Broken Ceiling Light",
                Description = "Fluorescent light fixture on Floor 3 is flickering and needs to be replaced",
                Priority = "Low",
                Status = "Resolved",
                ReportedDate = DateTime.UtcNow.AddDays(-10),
                ScheduledDate = DateTime.UtcNow.AddDays(-9),
                Location = "Floor 3, Corridor",
                IsOnSite = true,
                CreatedByUserId = users[1].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        context.Reports.AddRange(reports);
        context.SaveChanges();

        // Create test events
        var events = new List<OrganizationEvent>
        {
            new OrganizationEvent
            {
                Title = "Team Lunch & Networking",
                Description = "Monthly team gathering for lunch and casual networking. All staff welcome!",
                EventDate = new DateTime(2026, 6, 20, 12, 0, 0),
                EventEndDate = new DateTime(2026, 6, 20, 13, 30, 0),
                Location = "Main Cafeteria",
                IsOnSite = true,
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new OrganizationEvent
            {
                Title = "Quarterly All-Hands Meeting",
                Description = "CEO will present quarterly results and discuss company strategy for next quarter",
                EventDate = new DateTime(2026, 6, 25, 10, 0, 0),
                EventEndDate = new DateTime(2026, 6, 25, 11, 30, 0),
                Location = "Auditorium",
                IsOnSite = true,
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new OrganizationEvent
            {
                Title = "Training: ASP.NET Core Advanced",
                Description = "Professional development workshop on advanced ASP.NET Core topics including performance optimization",
                EventDate = new DateTime(2026, 6, 27, 14, 0, 0),
                EventEndDate = new DateTime(2026, 6, 27, 17, 0, 0),
                Location = "Training Room B",
                IsOnSite = true,
                CreatedByUserId = users[1].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new OrganizationEvent
            {
                Title = "Company Anniversary Party",
                Description = "Celebrate company's 10th anniversary with food, entertainment, and awards ceremony",
                EventDate = new DateTime(2026, 7, 15, 18, 0, 0),
                EventEndDate = new DateTime(2026, 7, 15, 22, 0, 0),
                Location = "Grand Ballroom Hotel",
                IsOnSite = false,
                CreatedByUserId = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };

        context.OrganizationEvents.AddRange(events);
        context.SaveChanges();
    }
}
