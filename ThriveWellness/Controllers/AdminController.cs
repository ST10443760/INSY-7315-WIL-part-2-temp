using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin landing pages - the dashboard, the calendar and
// the cross-session waitlist overview - plus the two waitlist actions that
// don't belong to a specific session (notify, remove). Everything here is
// read-only except the last two actions, so it leans on
// AdminDashboardService for its queries rather than touching repositories
// directly.
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class AdminController : Controller
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly IWaitlistService _waitlistService;
    private readonly IScheduledNotificationService _scheduledNotificationService;

    public AdminController(
        IAdminDashboardService dashboardService,
        IWaitlistService waitlistService,
        IScheduledNotificationService scheduledNotificationService)
    {
        _dashboardService = dashboardService;
        _waitlistService = waitlistService;
        _scheduledNotificationService = scheduledNotificationService;
    }

    // Also where [Authorize] sends an admin after a successful login - see
    // AccountController.Login.
    public async Task<IActionResult> Index()
    {
        var dashboard = await _dashboardService.GetDashboardAsync();
        return View(dashboard);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Runs the same day-before reminder sweep the hourly background service
    // runs, once, right now - safe to press repeatedly, since anyone already
    // reminded is skipped rather than emailed again.
    public async Task<IActionResult> SendRemindersNow()
    {
        var result = await _scheduledNotificationService.SendDueRemindersAsync();

        TempData["DashboardBannerType"] = result.Failed > 0 ? "notice" : "success";
        TempData["DashboardBannerMessage"] = result.ToSummary();

        return RedirectToAction(nameof(Index));
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

    // The email log: the latest 50 Notifications rows, optionally filtered
    // to one type. An unknown type is treated as "all" rather than showing
    // an empty page for a mistyped URL.
    public async Task<IActionResult> Emails(string? type)
    {
        var filterType = type != null && EmailTypes.All.Contains(type) ? type : null;

        var emails = await _dashboardService.GetRecentEmailsAsync(filterType);
        ViewData["SelectedType"] = filterType ?? "all";
        return View(emails);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Sends the one-off "you're getting close" courtesy email for a single
    // waitlist entry - see WaitlistService.NotifyAsync.
    public async Task<IActionResult> NotifyWaitlistEntry(int id)
    {
        var emailSent = await _waitlistService.NotifyAsync(id);
        if (!emailSent)
        {
            TempData["WaitlistBannerType"] = "notice";
            TempData["WaitlistBannerMessage"] = "The email could not be sent. Please try again later.";
        }
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Manually promotes one waitlist entry into an Awaiting-Payment booking
    // with a Pending payment - see WaitlistService.AddToClassAsync for the
    // guards (session full/closed/past, client already booked) and the
    // shared promotion routine this and automatic promotion both go through.
    public async Task<IActionResult> AddToClass(int id)
    {
        var result = await _waitlistService.AddToClassAsync(id);

        if (!result.Success)
        {
            TempData["WaitlistBannerType"] = "error";
            TempData["WaitlistBannerMessage"] = result.ErrorMessage;
        }
        else if (!result.EmailSent)
        {
            TempData["WaitlistBannerType"] = "notice";
            TempData["WaitlistBannerMessage"] = "Added to class, but the email could not be sent.";
        }
        else
        {
            TempData["WaitlistBannerType"] = "success";
            TempData["WaitlistBannerMessage"] = "Added to class. A payment details email has been sent.";
        }

        return RedirectToAction(nameof(Waitlist));
    }
}
