using BCryptNet = BCrypt.Net.BCrypt;
using WebApplication1.Models;

namespace WebApplication1.Data;

public static class SeedData
{
    public static void Initialize(ApplicationDbContext context)
    {
        // Ensure the default users exist and have known passwords.
        var adminUser = EnsureDefaultUser(context,
            username: "admin",
            password: "admin123",
            firstName: "Admin",
            lastName: "User",
            nickName: "Admin",
            email: "admin@organization.com",
            phone: "081-000-0000",
            line: "admin_line",
            role: "Admin",
            position: "Administrator",
            workLocation: "Floor 1, Desk 1");

        var somchaiUser = EnsureDefaultUser(context,
            username: "somchai",
            password: "pass123",
            firstName: "Somchai",
            lastName: "Developer",
            nickName: "Som",
            email: "somchai@organization.com",
            phone: "081-111-1111",
            line: "som_dev",
            role: "Manager",
            position: "Project Manager",
            workLocation: "Floor 2, Desk 5");

        var niranUser = EnsureDefaultUser(context,
            username: "niran",
            password: "pass123",
            firstName: "Niran",
            lastName: "Engineer",
            nickName: "Niran",
            email: "niran@organization.com",
            phone: "081-222-2222",
            line: "niran_eng",
            role: "User",
            position: "Software Engineer",
            workLocation: "Floor 2, Desk 10");

        var wilaiUser = EnsureDefaultUser(context,
            username: "wilai",
            password: "pass123",
            firstName: "Wilai",
            lastName: "Designer",
            nickName: "Wilai",
            email: "wilai@organization.com",
            phone: "081-333-3333",
            line: "wilai_design",
            role: "User",
            position: "UI/UX Designer",
            workLocation: "Floor 3, Desk 2");

        context.SaveChanges();

        if (!context.Projects.Any())
        {
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
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = adminUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-60)
                },
                new Project
                {
                    Name = "API Integration",
                    Description = "Integrate third-party payment and analytics APIs",
                    StartDate = new DateTime(2026, 4, 1),
                    EndDate = new DateTime(2026, 6, 15),
                    Status = "OnHold",
                    Issues = "Waiting for vendor API documentation update",
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = adminUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            context.Projects.AddRange(projects);
            context.SaveChanges();
        }

        if (!context.Reports.Any())
        {
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
                    CreatedByUserId = somchaiUser.Id,
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
                    CreatedByUserId = niranUser.Id,
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
                    CreatedByUserId = wilaiUser.Id,
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
                    CreatedByUserId = somchaiUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            context.Reports.AddRange(reports);
            context.SaveChanges();
        }

        if (!context.OrganizationEvents.Any())
        {
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
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = adminUser.Id,
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
                    CreatedByUserId = somchaiUser.Id,
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
                    CreatedByUserId = adminUser.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-15)
                }
            };

            context.OrganizationEvents.AddRange(events);
            context.SaveChanges();
        }
    }

    private static User EnsureDefaultUser(
        ApplicationDbContext context,
        string username,
        string password,
        string firstName,
        string lastName,
        string nickName,
        string email,
        string phone,
        string line,
        string role,
        string position,
        string workLocation)
    {
        var user = context.Users.FirstOrDefault(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                PasswordHash = BCryptNet.HashPassword(password),
                FirstName = firstName,
                LastName = lastName,
                NickName = nickName,
                Email = email,
                Phone = phone,
                Line = line,
                Role = role,
                Position = position,
                WorkLocation = workLocation,
                Status = "Available",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
        }
        else
        {
            user.PasswordHash = BCryptNet.HashPassword(password);
            user.FirstName = firstName;
            user.LastName = lastName;
            user.NickName = nickName;
            user.Email = email;
            user.Phone = phone;
            user.Line = line;
            user.Role = role;
            user.Position = position;
            user.WorkLocation = workLocation;
            user.Status = "Available";
            user.IsActive = true;
            if (user.CreatedAt == default)
            {
                user.CreatedAt = DateTime.UtcNow;
            }
        }
        return user;
    }
}
