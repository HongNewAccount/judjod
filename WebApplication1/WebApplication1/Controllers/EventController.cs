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

    public async Task<IActionResult> Index()
    {
        var events = await _context.OrganizationEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Attendees)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();

        return View(events);
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
