using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly IScheduledNotificationService _scheduledNotificationService;
    private readonly IAdminDashboardService _dashboardService;

    public AdminController(
        IScheduledNotificationService scheduledNotificationService,
        IAdminDashboardService dashboardService)
    {
        _scheduledNotificationService = scheduledNotificationService;
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetDashboardAsync();
        return View(dashboard);
    }

    public async Task<IActionResult> Calendar()
    {
        var days = await _dashboardService.GetCalendarAsync();
        return View(days);
    }

    public async Task<IActionResult> Waitlist()
    {
        var entries = await _dashboardService.GetWaitlistOverviewAsync();
        return View(entries);
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
