﻿using Homies.Data;
using Homies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace Homies.Controllers;
[Authorize]
public class EventController : Controller
{
    private readonly HomiesDbContext data;

    public EventController(HomiesDbContext context)
    {
        data = context;
    }
    public async Task<IActionResult> All()
    {
        var events = await data.Events
            .AsNoTracking()
            .Select(e => new EventInfoViewModel(
                e.Id,
                e.Name,
                e.Start,
                e.Type.Name,
                e.Organiser.UserName))
            .ToListAsync();

        return View(events);
    }

    public async Task<IActionResult> Join(int id)
    {
        var e = await data.Events
            .Where(e => e.Id == id)
            .Include(e => e.EventsParticipants)
            .FirstOrDefaultAsync();

        if (e == null)
        {
            return BadRequest();
        }

        string userId = GetUserId();

        if (!e.EventsParticipants.Any(ep => ep.HelperId == userId))
        {
            e.EventsParticipants.Add(new EventParticipant
            {
                EventId = id,
                HelperId = userId
            });

            await data.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Joined));
    }
    [HttpGet]
    public async Task<IActionResult> Joined()
    {
        var userId = GetUserId();

        var model = await data.EventParticipants
            .Where(ep => ep.HelperId == userId)
            .AsNoTracking()
            .Select(ep => new EventInfoViewModel(
              ep.Event.Id,
              ep.Event.Name,
              ep.Event.Start,
              ep.Event.Type.Name,
              ep.Event.Organiser.UserName))
            .ToListAsync();

        return View(model);
    }

    public async Task<IActionResult> Leave(int id)
    {
        var e = await data.Events
            .Where(e => e.Id == id)
            .Include(e => e.EventsParticipants)
            .FirstOrDefaultAsync();

        if (e == null)
        {
            return BadRequest();
        }

        var userId = GetUserId();

        var ep = e.EventsParticipants
            .FirstOrDefault(ep => ep.HelperId == userId);

        if (ep == null)
        {
            return BadRequest();
        }

        e.EventsParticipants.Remove(ep);

        await data.SaveChangesAsync();

        return RedirectToAction(nameof(All));
    }

    [HttpGet]
    public async Task<IActionResult> Add()
    {
        var model = new EventFormViewModel();
        model.Types = await GetTypes();

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Add(EventFormViewModel model)
    {
        DateTime start = DateTime.Now;
        DateTime end = DateTime.Now;

        if (!DateTime.TryParseExact(model.Start, DataConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            ModelState.AddModelError(nameof(model.Start), $"Invalid date! Format must be: {DataConstants.DateFormat}.");
        }

        if (!DateTime.TryParseExact(model.End, DataConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
        {
            ModelState.AddModelError(nameof(model.End), $"Invalid date! Format must be: {DataConstants.DateFormat}.");
        }

        if (!ModelState.IsValid)
        {
            model.Types = await GetTypes();
            return View(model);
        }

        var entity = new Event
        {
            CreatedOn = DateTime.Now,
            Description = model.Description,
            Name = model.Name,
            OrganiserId = GetUserId(),
            TypeId =  model.TypeId,
            Start = start,
            End = end
        };
        await data.Events.AddAsync(entity);
        await data.SaveChangesAsync();

        return RedirectToAction(nameof(All));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await data.Events.FindAsync(id);

        if (e == null) 
        {
            return BadRequest();
        }

        if (e.OrganiserId != GetUserId())
        {
            return Unauthorized();
        }

        var model = new EventFormViewModel
        {
            Name = e.Name,
            Description = e.Description,
            Start = e.Start.ToString(DataConstants.DateFormat),
            End = e.End.ToString(DataConstants.DateFormat),
            Types = await GetTypes(),
            TypeId = e.TypeId,
        };

        return View (model);
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        var e = await data.Events.FindAsync(id);

        if (e == null)
        {
            return BadRequest();
        }

        if (e.OrganiserId != GetUserId())
        {
            return Unauthorized();
        }

        DateTime start = DateTime.Now;
        DateTime end = DateTime.Now;

        if (!DateTime.TryParseExact(model.Start, DataConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            ModelState.AddModelError(nameof(model.Start), $"Invalid date! Format must be: {DataConstants.DateFormat}.");
        }

        if (!DateTime.TryParseExact(model.End, DataConstants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
        {
            ModelState.AddModelError(nameof(model.End), $"Invalid date! Format must be: {DataConstants.DateFormat}.");
        }

        if (!ModelState.IsValid)
        {
            model.Types = await GetTypes();
            return View(model);
        }

        e.Name = model.Name;
        e.Description = model.Description;
        e.Start = start;
        e.End = end;
        e.TypeId = model.TypeId;

        await data.SaveChangesAsync();

        return RedirectToAction(nameof(All));
    }

    public async Task<IActionResult> Details(int id)
    {
        var model = await data.Events
            .Where(e => e.Id == id)
            .AsNoTracking()
            .Select(e => new EventDetailsViewModel
            {
                Name = e.Name,
                Description = e.Description,
                Start = e.Start.ToString(DataConstants.DateFormat),
                End = e.End.ToString(DataConstants.DateFormat),
                CreatedOn = e.CreatedOn.ToString(DataConstants.DateFormat),
                Organiser = e.Organiser.UserName,
                Type = e.Type.Name,
                Id = e.Id
            })
            .FirstOrDefaultAsync();

        if (model == null)
        {
            return BadRequest();
        }

        return View(model);
            
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    private async Task<IEnumerable<TypeViewModel>> GetTypes()
    {
        return await data.Types
            .AsNoTracking()
            .Select(t => new TypeViewModel
            {
                Id = t.Id,
                Name = t.Name
            })
            .ToListAsync();
    }

}
