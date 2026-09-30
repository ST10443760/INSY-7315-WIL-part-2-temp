using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IWaitlistService
    {
        Task<Waitlist> JoinWaitlistAsync(int clientId, int sessionId);
        Task PromoteNextInLineAsync(int sessionId);

        // Admin removes one waitlist entry directly (not a promotion - no
        // booking is created), renumbering the remaining entries for that
        // session so positions stay contiguous from 1.
        Task RemoveAsync(int waitlistId);

        // Admin-triggered courtesy email to one waitlisted client - see
        // INotificationService.SendWaitlistCourtesyEmailAsync.
        Task NotifyAsync(int waitlistId);
    }
}
