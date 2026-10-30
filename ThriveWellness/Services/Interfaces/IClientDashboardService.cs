using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IClientDashboardService
    {
        Task<ClientDashboardViewModel> GetDashboardAsync(int clientId, string firstName);
    }
}
