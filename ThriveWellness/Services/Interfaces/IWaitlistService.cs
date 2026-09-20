using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IWaitlistService
    {
        Task<Waitlist> JoinWaitlistAsync(int clientId, int sessionId);
    }
}
