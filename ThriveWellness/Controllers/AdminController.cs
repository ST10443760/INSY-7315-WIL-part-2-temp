using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly IScheduledNotificationService _scheduledNotificationService;
    private readonly IAdminDashboardService _dashboardService;
    private readonly IWaitlistService _waitlistService;

    public AdminController(
        IScheduledNotificationService scheduledNotificationService,
        IAdminDashboardService dashboardService,
        IWaitlistService waitlistService)
    {
        _scheduledNotificationService = scheduledNotificationService;
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

    // TEMP — remove before final submission.
    // Manually runs a scheduled notification job on demand (the real ones only
    // run on the hourly background timer). jobName is "reminders" or "location".
    // Admin-only via the class-level [Authorize].
    [HttpPost("Admin/DebugTrigger/{jobName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DebugTrigger(string jobName)
    {
        switch (jobName)
        {
            case "reminders":
                await _scheduledNotificationService.SendDueRemindersAsync();
                break;
            case "location":
                await _scheduledNotificationService.SendDueLocationEmailsAsync();
                break;
            default:
                return BadRequest($"Unknown job '{jobName}'. Use 'reminders' or 'location'.");
        }

        return Ok($"Ran '{jobName}'.");
    }
}
