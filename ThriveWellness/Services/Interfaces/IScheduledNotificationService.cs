using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IScheduledNotificationService
    {
        Task<ReminderRunResult> SendDueRemindersAsync();
        Task SendDueLocationEmailsAsync();
    }
}
