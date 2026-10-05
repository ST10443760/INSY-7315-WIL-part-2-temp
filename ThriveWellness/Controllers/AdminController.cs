using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IWaitlistService _waitlistService;

    public AdminController(
        IAdminDashboardService dashboardService,
        IWaitlistService waitlistService)
    {
        _dashboardService = dashboardService;
        _waitlistService = waitlistService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> Calendar(int? year, int? month)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (m is < 1 or > 12)
        {
            return BadRequest();
        }

        var days = await _dashboardService.GetCalendarAsync(y, m);

        var viewModel = new AdminCalendarViewModel
        {
            Year = y,
            Month = m,
            Days = days
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Waitlist()
    {
        var entries = await _dashboardService.GetWaitlistOverviewAsync();
        return View(entries);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyWaitlistEntry(int id)
    {
        await _waitlistService.NotifyAsync(id);
        return RedirectToAction(nameof(Waitlist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveWaitlistEntry(int id)
    {
        await _waitlistService.RemoveAsync(id);
        return RedirectToAction(nameof(Waitlist));
    }
}
