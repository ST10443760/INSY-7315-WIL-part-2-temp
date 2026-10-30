using ThriveWellness.Models;

namespace ThriveWellness.Services.Interfaces
{
    public interface IRevenueService
    {
        // Turns the page's preset ("month", "30days", "year", "all") or
        // custom from/to dates into the range to report on.
        RevenueRange ResolveRange(string? preset, DateTime? from, DateTime? to);

        Task<RevenueReport> GetReportAsync(RevenueRange range);
    }
}
