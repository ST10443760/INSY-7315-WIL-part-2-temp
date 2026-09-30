using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardViewModel> GetDashboardAsync();
        Task<IReadOnlyList<SessionOverviewViewModel>> GetSessionOverviewsAsync();

        // Every day of the given month, in order, whether or not it has any
        // sessions - the view builds the Sun-Sat grid (and leading/trailing
        // blank cells) from this.
        Task<IReadOnlyList<CalendarDayViewModel>> GetCalendarAsync(int year, int month);
        Task<IReadOnlyList<WaitlistOverviewRowViewModel>> GetWaitlistOverviewAsync();
    }
}
