using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin landing pages - the dashboard, the calendar and
// the cross-session waitlist overview - plus the two waitlist actions that
// don't belong to a specific session (notify, remove). Everything here is
// read-only except the last two actions, so it leans on
// AdminDashboardService for its queries rather than touching repositories
// directly.
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

    // Also where [Authorize] sends an admin after a successful login - see
    // AccountController.Login.
    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetDashboardAsync();
        return View(dashboard);
    }

    // Defaults to the current month if none is given; rejects an
    // out-of-range month rather than letting GetCalendarAsync construct an
    // invalid DateTime.
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

    // Every session's waitlist in one list - contrast
    // SessionController.Waitlist, which is scoped to a single session.
    public async Task<IActionResult> Waitlist()
    {
        var entries = await _dashboardService.GetWaitlistOverviewAsync();
        return View(entries);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Sends the one-off "you're getting close" courtesy email for a single
    // waitlist entry - see WaitlistService.NotifyAsync.
    public async Task<IActionResult> NotifyWaitlistEntry(int id)
    {
        await _waitlistService.NotifyAsync(id);
        return RedirectToAction(nameof(Waitlist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Manually removes one waitlist entry without promoting them into a
    // booking - see WaitlistService.RemoveAsync.
    public async Task<IActionResult> RemoveWaitlistEntry(int id)
    {
        await _waitlistService.RemoveAsync(id);
        return RedirectToAction(nameof(Waitlist));
    }
}
