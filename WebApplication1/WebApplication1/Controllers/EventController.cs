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

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 20;

        var query = _context.OrganizationEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Attendees)
            .OrderByDescending(e => e.EventDate);

        var totalCount = await query.CountAsync();
        var events = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

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
                @event.UpdatedAt = DateTime.UtcNow;
                _context.Update(@event);
                await _context.SaveChangesAsync();
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
