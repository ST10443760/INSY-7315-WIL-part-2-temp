using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardAsync();
        Task<IReadOnlyList<SessionOverviewViewModel>> GetSessionOverviewsAsync();
        Task<IReadOnlyList<CalendarDayViewModel>> GetCalendarAsync();
        Task<IReadOnlyList<WaitlistOverviewRowViewModel>> GetWaitlistOverviewAsync();
    }
}
