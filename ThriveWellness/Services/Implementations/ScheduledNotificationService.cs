using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    public class ScheduledNotificationService : IScheduledNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ScheduledNotificationService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task SendDueRemindersAsync()
        {
            var tomorrow = DateTime.UtcNow.Date.AddDays(1);

            var dueBookings = await (
                from booking in _context.Bookings
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                where booking.Status == "Confirmed" && session.Date.Date == tomorrow
                where !_context.Notifications.Any(n => n.BookingId == booking.BookingId && n.Type == "Reminder")
                select booking
            ).ToListAsync();

            foreach (var booking in dueBookings)
            {
                await _notificationService.SendReminderEmailAsync(booking);
            }
        }

        public Task SendDueLocationEmailsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
