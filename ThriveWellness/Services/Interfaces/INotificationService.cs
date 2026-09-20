using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendWelcomeEmailAsync(Booking booking);
    }
}
