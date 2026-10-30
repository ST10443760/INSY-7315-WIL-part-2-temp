using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IRewardsService
    {
        // Awards any badges the client has newly earned and returns their
        // codes. Safe to call as often as you like: a badge is never
        // awarded twice.
        Task<IReadOnlyList<string>> EvaluateAsync(int clientId);

        Task<RewardsViewModel> GetRewardsAsync(int clientId);
    }
}
