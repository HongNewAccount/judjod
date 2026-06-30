using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class EventController : Controller
{
    private readonly ApplicationDbContext _context;

    public EventController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string searchTerm = "", string sortBy = "latest", string filter = "all", int page = 1)
    {
        const int pageSize = 9;
        var events = await _context.OrganizationEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Attendees)
            .ToListAsync();

        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearchTerm = searchTerm.ToLower();
            events = events.Where(e =>
                e.Title.ToLower().Contains(lowerSearchTerm) ||
                e.Description.ToLower().Contains(lowerSearchTerm) ||
                e.Location.ToLower().Contains(lowerSearchTerm)
            ).ToList();
        }

        // Statistics (reflect search, not the timeline filter)
        var now = DateTime.UtcNow;
        var stats = new Dictionary<string, int>
        {
            { "TotalEvents", events.Count },
            { "UpcomingEvents", events.Count(e => e.EventDate > now) },
            { "PastEvents", events.Count(e => e.EventDate <= now) }
        };

        // Timeline filter
        if (filter == "upcoming")
        {
            events = events.Where(e => e.EventDate > now).ToList();
        }
        else if (filter == "past")
        {
            events = events.Where(e => e.EventDate <= now).ToList();
        }
        else if (filter == "thismonth")
        {
            events = events.Where(e => e.EventDate.Year == now.Year && e.EventDate.Month == now.Month).ToList();
        }

        // Sort
        events = sortBy switch
        {
            "oldest" => events.OrderBy(e => e.EventDate).ToList(),
            "title-asc" => events.OrderBy(e => e.Title).ToList(),
            "title-desc" => events.OrderByDescending(e => e.Title).ToList(),
            _ => events.OrderByDescending(e => e.EventDate).ToList()
        };

        var totalCount = events.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));
        var pagedEvents = events.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Stats = stats;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.SortBy = sortBy;
        ViewBag.FilterType = filter;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(pagedEvents);
    }

    public async Task<IActionResult> Details(int id)
    {
        var @event = await _context.OrganizationEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Attendees)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (@event == null)
            return NotFound();

        return View(@event);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrganizationEvent @event)
    {
        if (ModelState.IsValid)
        {
            @event.CreatedByUserId = 1; // TODO: Get from session/user context
            _context.Add(@event);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(@event);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var @event = await _context.OrganizationEvents.FindAsync(id);
        if (@event == null)
            return NotFound();

        return View(@event);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrganizationEvent @event)
    {
        if (id != @event.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingEvent = await _context.OrganizationEvents.FindAsync(id);
                if (existingEvent != null)
                {
                    existingEvent.Title = @event.Title;
                    existingEvent.Description = @event.Description;
                    existingEvent.EventDate = @event.EventDate;
                    existingEvent.EventEndDate = @event.EventEndDate;
                    existingEvent.Location = @event.Location;
                    existingEvent.IsOnSite = @event.IsOnSite;
                    existingEvent.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existingEvent);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Index));
        }

        return View(@event);
    }
}
