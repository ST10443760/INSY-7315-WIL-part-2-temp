namespace ThriveWellness.Services.Interfaces
{
    public interface IScheduledNotificationService
    {
        Task SendDueRemindersAsync();
        Task SendDueLocationEmailsAsync();
    }
}
