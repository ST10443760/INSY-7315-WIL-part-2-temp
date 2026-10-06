using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IWaitlistService
    {
        Task<Waitlist> JoinWaitlistAsync(int clientId, int sessionId);
        Task PromoteNextInLineAsync(int sessionId);

        // Admin-triggered: promotes one specific waitlist entry into a real,
        // Awaiting-Payment booking with a Pending payment, independent of
        // where it sits in the queue. Validates the session and client
        // still exist, and that the session isn't full, closed or in the
        // past, and that the client doesn't already have an active booking
        // for it - makes no changes if any of those fail.
        Task<AddToClassResult> AddToClassAsync(int waitlistId);

        // Admin removes one waitlist entry directly (not a promotion - no
        // booking is created), renumbering the remaining entries for that
        // session so positions stay contiguous from 1.
        Task RemoveAsync(int waitlistId);

        // Admin-triggered courtesy email to one waitlisted client - see
        // INotificationService.SendWaitlistCourtesyEmailAsync.
        Task NotifyAsync(int waitlistId);
    }
}
