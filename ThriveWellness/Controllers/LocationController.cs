using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: standard admin CRUD for studio locations. Every write
// action catches LocationService's ArgumentException and turns it into a
// form error (Create/Edit) or a TempData message (Delete) rather than
// letting it become an unhandled 500 - that's how the "name/address
// required" and "location has sessions tied to it" rules actually reach the
// admin's screen.
[Authorize]
public class LocationController : Controller
{
    private readonly ILocationService _locationService;

    public LocationController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    public async Task<IActionResult> Index()
    {
        var locations = await _locationService.GetAllAsync();
        return View(locations);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new LocationFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LocationFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var location = new Location
        {
            Name = model.Name,
            Address = model.Address
        };

        try
        {
            await _locationService.CreateAsync(location);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var location = await _locationService.GetByIdAsync(id);
        if (location == null)
        {
            return NotFound();
        }

        var viewModel = new LocationFormViewModel
        {
            LocationId = location.LocationId,
            Name = location.Name,
            Address = location.Address
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LocationFormViewModel model)
    {
        if (id != model.LocationId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var location = new Location
        {
            LocationId = model.LocationId,
            Name = model.Name,
            Address = model.Address
        };

        try
        {
            await _locationService.UpdateAsync(location);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _locationService.DeleteAsync(id);
        }
        catch (ArgumentException ex)
        {
            TempData["LocationDeleteError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
