using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly IScheduledNotificationService _scheduledNotificationService;

    public AdminController(IScheduledNotificationService scheduledNotificationService)
    {
        _scheduledNotificationService = scheduledNotificationService;
    }

    public IActionResult Index()
    {
        return View();
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
