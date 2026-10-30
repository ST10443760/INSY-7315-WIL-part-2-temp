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

        // Classes attended, current streak and badges earned for every
        // client, keyed by client id, for the admin Clients page. Clients
        // with no bookings or badges simply aren't in it.
        Task<IReadOnlyDictionary<int, ClientRewardsSummary>> GetSummariesAsync();
    }
}
