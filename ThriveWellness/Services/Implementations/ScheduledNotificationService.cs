using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: the two queries behind the scheduled email sweep -
    // "which confirmed bookings are for a class tomorrow and haven't had a
    // reminder yet" and "which confirmed, first-time-client bookings are for
    // a class today and haven't had a location email yet". Pure query +
    // send; actually running this on a timer lives in
    // ScheduledNotificationHostedService alongside this file.
    public class ScheduledNotificationService : IScheduledNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ScheduledNotificationService> _logger;

        public ScheduledNotificationService(
            ApplicationDbContext context,
            INotificationService notificationService,
            ILogger<ScheduledNotificationService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _logger = logger;
        }

        // Finds every Confirmed booking for a session happening tomorrow
        // that hasn't already had a Reminder notification logged, and sends
        // one to each - the Notifications check is what makes this safe to
        // run repeatedly (every hour, or from the admin's "Send reminders
        // now" button) without double-emailing anyone. Bookings that were
        // already reminded count as skipped. One failed send is logged and
        // counted rather than stopping the rest of the run; it has no
        // Reminder row, so the next run picks it up again.
        public async Task<ReminderRunResult> SendDueRemindersAsync()
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);

            var candidates = await (
                from booking in _context.Bookings
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                where booking.Status == "Confirmed" && session.Date.Date == tomorrow
                select new
                {
                    Booking = booking,
                    AlreadyReminded = _context.Notifications.Any(n => n.BookingId == booking.BookingId && n.Type == "Reminder")
                }
            ).ToListAsync();

            var result = new ReminderRunResult
            {
                Skipped = candidates.Count(c => c.AlreadyReminded)
            };

            foreach (var booking in candidates.Where(c => !c.AlreadyReminded).Select(c => c.Booking))
            {
                try
                {
                    await _notificationService.SendReminderEmailAsync(booking);
                    result.Sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder email failed for booking {BookingId}", booking.BookingId);
                    result.Failed++;
                }
            }

            return result;
        }

        // Same idea as the reminder sweep above, but for sessions happening
        // today, restricted to clients still marked as new (returning
        // clients already know where to go), and logged as a Location
        // notification instead.
        public async Task SendDueLocationEmailsAsync()
        {
            var today = DateTime.UtcNow.Date;

            var dueBookings = await (
                from booking in _context.Bookings
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join client in _context.Clients on booking.ClientId equals client.ClientId
                where booking.Status == "Confirmed" && session.Date.Date == today && client.IsNew
                where !_context.Notifications.Any(n => n.BookingId == booking.BookingId && n.Type == "Location")
                select booking
            ).ToListAsync();

            foreach (var booking in dueBookings)
            {
                await _notificationService.SendLocationEmailAsync(booking);
            }
        }
    }
}
