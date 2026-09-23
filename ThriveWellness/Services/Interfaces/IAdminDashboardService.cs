using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardAsync();
    }
}
