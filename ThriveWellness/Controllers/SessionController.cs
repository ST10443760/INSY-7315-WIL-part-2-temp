using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class SessionController : Controller
{
    private readonly ISessionService _sessionService;
    private readonly ILocationRepository _locationRepository;

    public SessionController(ISessionService sessionService, ILocationRepository locationRepository)
    {
        _sessionService = sessionService;
        _locationRepository = locationRepository;
    }

    public async Task<IActionResult> Index()
    {
        var sessions = await _sessionService.GetAllAsync();
        var locations = (await _locationRepository.GetAllAsync()).ToDictionary(l => l.LocationId, l => l.Name);

        var viewModel = sessions.Select(s => new SessionListItemViewModel
        {
            SessionId = s.SessionId,
            LocationName = locations.TryGetValue(s.LocationId, out var name) ? name : "Unknown",
            SessionType = s.SessionType,
            Date = s.Date,
            Time = s.Time,
            Capacity = s.Capacity,
            IsOpen = s.IsOpen
        }).OrderBy(s => s.Date).ThenBy(s => s.Time);

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new SessionFormViewModel
        {
            Locations = await _locationRepository.GetAllAsync()
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SessionFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Locations = await _locationRepository.GetAllAsync();
            return View(model);
        }

        var session = new Session
        {
            LocationId = model.LocationId,
            SessionType = model.SessionType,
            Date = model.Date,
            Time = model.Time,
            Capacity = model.Capacity,
            IsOpen = model.IsOpen
        };

        try
        {
            await _sessionService.CreateAsync(session);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Locations = await _locationRepository.GetAllAsync();
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var session = await _sessionService.GetByIdAsync(id);
        if (session == null)
        {
            return NotFound();
        }

        var viewModel = new SessionFormViewModel
        {
            SessionId = session.SessionId,
            LocationId = session.LocationId,
            SessionType = session.SessionType,
            Date = session.Date,
            Time = session.Time,
            Capacity = session.Capacity,
            IsOpen = session.IsOpen,
            Locations = await _locationRepository.GetAllAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SessionFormViewModel model)
    {
        if (id != model.SessionId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            model.Locations = await _locationRepository.GetAllAsync();
            return View(model);
        }

        var session = new Session
        {
            SessionId = model.SessionId,
            LocationId = model.LocationId,
            SessionType = model.SessionType,
            Date = model.Date,
            Time = model.Time,
            Capacity = model.Capacity,
            IsOpen = model.IsOpen
        };

        try
        {
            await _sessionService.UpdateAsync(session);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            model.Locations = await _locationRepository.GetAllAsync();
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _sessionService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkFull(int id)
    {
        await _sessionService.MarkAsFullAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkOpen(int id)
    {
        await _sessionService.MarkAsOpenAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
