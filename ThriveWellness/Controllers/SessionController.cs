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
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IAdminDashboardService _dashboardService;

    public SessionController(
        ISessionService sessionService,
        ILocationRepository locationRepository,
        IWaitlistRepository waitlistRepository,
        IClientRepository clientRepository,
        IAdminDashboardService dashboardService)
    {
        _sessionService = sessionService;
        _locationRepository = locationRepository;
        _waitlistRepository = waitlistRepository;
        _clientRepository = clientRepository;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        // Same overview data as the dashboard/calendar, so "Full" means the
        // same thing everywhere (closed by an admin OR booked >= capacity).
        var sessions = await _dashboardService.GetSessionOverviewsAsync();
        return View(sessions);
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

    [HttpGet]
    public async Task<IActionResult> Waitlist(int id)
    {
        var session = await _sessionService.GetByIdAsync(id);
        if (session == null)
        {
            return NotFound();
        }

        var entries = await _waitlistRepository.GetBySessionAsync(id);

        var viewModel = new List<WaitlistEntryViewModel>();
        foreach (var entry in entries)
        {
            var client = await _clientRepository.GetByIdAsync(entry.ClientId);
            viewModel.Add(new WaitlistEntryViewModel
            {
                Position = entry.Position,
                ClientName = client?.FullName ?? "Unknown",
                ClientEmail = client?.Email ?? string.Empty,
                DateAdded = entry.DateAdded
            });
        }

        ViewData["SessionType"] = session.SessionType;
        ViewData["SessionDate"] = session.Date;
        ViewData["SessionTime"] = session.Time;

        return View(viewModel);
    }
}
