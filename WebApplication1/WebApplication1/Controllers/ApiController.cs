using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;

namespace WebApplication1.Controllers;

[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public ApiController(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    private bool IsAuthorized()
    {
        var key = _config["ApiKey"];
        if (string.IsNullOrEmpty(key)) return false;
        Request.Headers.TryGetValue("X-API-Key", out var fromHeader);
        var fromQuery = Request.Query["key"].ToString();
        return fromHeader == key || fromQuery == key;
    }

    // GET /api/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var totalProjects = await _context.Projects.CountAsync();
        var byStatus = await _context.Projects
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key ?? "Unknown", Count = g.Count() })
            .ToListAsync();
        var byPriority = await _context.Projects
            .GroupBy(p => p.Priority)
            .Select(g => new { Priority = g.Key ?? "None", Count = g.Count() })
            .ToListAsync();
        var avgProgress = await _context.Projects.AverageAsync(p => (double)p.Progress);
        var totalUsers = await _context.Users.CountAsync(u => u.IsActive);

        return Ok(new
        {
            TotalProjects = totalProjects,
            TotalActiveUsers = totalUsers,
            AverageProgress = Math.Round(avgProgress, 1),
            ByStatus = byStatus,
            ByPriority = byPriority
        });
    }

    // GET /api/projects?status=InProgress&priority=High
    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects([FromQuery] string? status, [FromQuery] string? priority)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var query = _context.Projects
            .Include(p => p.Owners).ThenInclude(o => o.User)
            .Include(p => p.CreatedByUser)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(priority))
            query = query.Where(p => p.Priority == priority);

        var projects = await query
            .OrderBy(p => p.SortOrder)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Status,
                p.Priority,
                p.Progress,
                p.StartDate,
                p.EndDate,
                p.CreatedAt,
                p.UpdatedAt,
                CreatedBy = p.CreatedByUser == null ? null : p.CreatedByUser.Username,
                Owners = p.Owners.Select(o => new
                {
                    o.UserId,
                    Username = o.User == null ? null : o.User.Username,
                    FullName = o.User == null ? null : $"{o.User.FirstName} {o.User.LastName}".Trim()
                })
            })
            .ToListAsync();

        return Ok(projects);
    }

    // GET /api/projects/{id}
    [HttpGet("projects/{id}")]
    public async Task<IActionResult> GetProject(int id)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var project = await _context.Projects
            .Include(p => p.Owners).ThenInclude(o => o.User)
            .Include(p => p.CreatedByUser)
            .Include(p => p.ActivityLogs).ThenInclude(a => a.User)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Status,
                p.Priority,
                p.Progress,
                p.Issues,
                p.StartDate,
                p.EndDate,
                p.CreatedAt,
                p.UpdatedAt,
                CreatedBy = p.CreatedByUser == null ? null : p.CreatedByUser.Username,
                Owners = p.Owners.Select(o => new
                {
                    o.UserId,
                    Username = o.User == null ? null : o.User.Username,
                    FullName = o.User == null ? null : $"{o.User.FirstName} {o.User.LastName}".Trim()
                }),
                RecentActivity = p.ActivityLogs
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(10)
                    .Select(a => new
                    {
                        a.ActionType,
                        a.Description,
                        a.CreatedAt,
                        By = a.User == null ? null : a.User.Username
                    })
            })
            .FirstOrDefaultAsync();

        if (project == null) return NotFound(new { error = $"Project {id} not found." });
        return Ok(project);
    }

    // GET /api/users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var users = await _context.Users
            .OrderBy(u => u.Username)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FirstName,
                u.LastName,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.Phone,
                u.Role,
                u.WorkLocation,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/users/{id}
    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.FirstName,
                u.LastName,
                FullName = $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.Phone,
                u.Role,
                u.WorkLocation,
                u.IsActive,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound(new { error = $"User {id} not found." });
        return Ok(user);
    }

    // GET /api/groups
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        var groups = await _context.ProjectGroups
            .Include(g => g.ProjectAssignments).ThenInclude(a => a.Project)
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Color,
                g.CreatedAt,
                Projects = g.ProjectAssignments.Select(a => new
                {
                    a.Project.Id,
                    a.Project.Name,
                    a.Project.Status,
                    a.Project.Progress
                })
            })
            .ToListAsync();

        return Ok(groups);
    }

    // GET /api/logs?limit=50
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs([FromQuery] int limit = 50)
    {
        if (!IsAuthorized()) return Unauthorized(new { error = "Invalid or missing API key." });

        if (limit > 200) limit = 200;

        var logs = await _context.ActivityLogs
            .Include(a => a.User)
            .Include(a => a.Project)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.ActionType,
                a.Description,
                a.OldValue,
                a.NewValue,
                a.CreatedAt,
                By = a.User == null ? null : a.User.Username,
                Project = a.Project == null ? null : a.Project.Name
            })
            .ToListAsync();

        return Ok(logs);
    }
}
