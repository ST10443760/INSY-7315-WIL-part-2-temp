using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: the admin Revenue page. Revenue is money actually kept:
    // paid (Confirmed) payments. Refunded payments are shown on their own
    // and never counted, and Refund due, Pending and declined ones aren't
    // revenue at all. GetReportAsync runs one query; Build (static, no
    // database) does all the adding up, so it's unit tested directly.
    // Amounts are decimal throughout.
    public class RevenueService : IRevenueService
    {
        public const int TopClientCount = 3;

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public RevenueService(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public RevenueRange ResolveRange(string? preset, DateTime? from, DateTime? to) =>
            ResolveRange(preset, from, to, _timeProvider.GetLocalNow().Date);

        // "month" is the 1st of this month to today, "30days" today and the
        // 29 days before it, "year" 1 January to today. Custom dates (either
        // or both) win over a preset, swapped if given backwards. Anything
        // else is All time.
        public static RevenueRange ResolveRange(string? preset, DateTime? from, DateTime? to, DateTime today)
        {
            if (from.HasValue || to.HasValue || string.Equals(preset, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var start = from?.Date;
                var end = to?.Date;
                if (start.HasValue && end.HasValue && start > end)
                {
                    (start, end) = (end, start);
                }
                return start == null && end == null
                    ? new RevenueRange(RevenuePreset.AllTime, null, null)
                    : new RevenueRange(RevenuePreset.Custom, start, end);
            }

            return preset?.ToLowerInvariant() switch
            {
                "month" => new RevenueRange(RevenuePreset.ThisMonth, new DateTime(today.Year, today.Month, 1), today),
                "30days" => new RevenueRange(RevenuePreset.Last30Days, today.AddDays(-29), today),
                "year" => new RevenueRange(RevenuePreset.ThisYear, new DateTime(today.Year, 1, 1), today),
                _ => new RevenueRange(RevenuePreset.AllTime, null, null)
            };
        }

        public async Task<RevenueReport> GetReportAsync(RevenueRange range)
        {
            var rows = await (
                from payment in _context.Payments
                join booking in _context.Bookings on payment.BookingId equals booking.BookingId
                join client in _context.Clients on booking.ClientId equals client.ClientId
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join location in _context.Locations on session.LocationId equals location.LocationId
                where payment.Status == PaymentStatuses.Confirmed || payment.Status == PaymentStatuses.Refunded
                select new RevenuePaymentRow(
                    payment.PaymentId, client.ClientId, client.FullName, client.Email,
                    payment.Amount, payment.Status, payment.Method, payment.PaymentType,
                    location.Name, session.Date)
            ).ToListAsync();

            return Build(rows, range);
        }

        public static RevenueReport Build(IEnumerable<RevenuePaymentRow> rows, RevenueRange range)
        {
            var inRange = rows
                .Where(r => (range.From == null || r.ClassDate.Date >= range.From.Value)
                         && (range.To == null || r.ClassDate.Date <= range.To.Value))
                .ToList();

            var paid = inRange.Where(r => r.Status == PaymentStatuses.Confirmed).ToList();
            var refunded = inRange.Where(r => r.Status == PaymentStatuses.Refunded).ToList();
            var net = paid.Sum(r => r.Amount);

            var clients = paid
                .GroupBy(r => r.ClientId)
                .Select(g => new ClientSpendRow
                {
                    ClientId = g.Key,
                    Name = g.First().ClientName,
                    Email = g.First().ClientEmail,
                    PaymentsCount = g.Count(),
                    TotalSpent = g.Sum(r => r.Amount),
                    LastPaymentDate = g.Max(r => r.ClassDate)
                })
                // Ties: more payments first, then by name, so the order (and
                // so the top three) never depends on database order.
                .OrderByDescending(c => c.TotalSpent)
                .ThenByDescending(c => c.PaymentsCount)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.ClientId)
                .ToList();

            return new RevenueReport
            {
                Range = range,
                NetRevenue = net,
                PaidCount = paid.Count,
                AveragePerPayment = paid.Count == 0 ? 0m : Math.Round(net / paid.Count, 2),
                TotalRefunded = refunded.Sum(r => r.Amount),
                RefundedCount = refunded.Count,
                ByMonth = ByMonth(paid, net),
                ByMethod = Breakdown(paid, net, r => MethodLabel(r.Method)),
                ByPaymentType = Breakdown(paid, net, r => PaymentTypeLabel(r.PaymentType)),
                ByVenue = Breakdown(paid, net, r => r.LocationName),
                Clients = clients,
                TopClients = clients.Take(TopClientCount).ToList()
            };
        }

        // One row per month from the first paid month to the last, months
        // with nothing paid included as zero so the chart has no gaps.
        private static IReadOnlyList<RevenueBreakdownRow> ByMonth(List<RevenuePaymentRow> paid, decimal net)
        {
            if (paid.Count == 0)
            {
                return Array.Empty<RevenueBreakdownRow>();
            }

            var byMonth = paid
                .GroupBy(r => new DateTime(r.ClassDate.Year, r.ClassDate.Month, 1))
                .ToDictionary(g => g.Key, g => (Amount: g.Sum(r => r.Amount), Count: g.Count()));

            var months = new List<RevenueBreakdownRow>();
            for (var month = byMonth.Keys.Min(); month <= byMonth.Keys.Max(); month = month.AddMonths(1))
            {
                var (amount, count) = byMonth.TryGetValue(month, out var total) ? total : (0m, 0);
                months.Add(new RevenueBreakdownRow { Label = month.ToString("MMM yyyy", CultureInfo.InvariantCulture), Amount = amount, Count = count, Share = ShareOf(amount, net) });
            }
            return months;
        }

        private static IReadOnlyList<RevenueBreakdownRow> Breakdown(List<RevenuePaymentRow> paid, decimal net, Func<RevenuePaymentRow, string> label)
        {
            return paid
                .GroupBy(label)
                .Select(g => new RevenueBreakdownRow { Label = g.Key, Amount = g.Sum(r => r.Amount), Count = g.Count(), Share = ShareOf(g.Sum(r => r.Amount), net) })
                .OrderByDescending(r => r.Amount)
                .ThenBy(r => r.Label)
                .ToList();
        }

        private static decimal ShareOf(decimal amount, decimal net) => net == 0 ? 0m : amount / net;

        public static string MethodLabel(string method) =>
            PaymentMethods.IsCard(method) ? "Card"
            : string.Equals(method, PaymentMethods.Eft, StringComparison.OrdinalIgnoreCase) ? "EFT"
            : string.Equals(method, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase) ? "Cash"
            : method;

        public static string PaymentTypeLabel(string paymentType) => paymentType switch
        {
            "per-class" => "Per class",
            "monthly" => "Monthly pass",
            _ => paymentType
        };
    }
}
