namespace ThriveWellness.Models
{
    public enum RevenuePreset
    {
        AllTime,
        ThisMonth,
        Last30Days,
        ThisYear,
        Custom
    }

    // The dates a revenue report covers, both ends inclusive. Null means
    // open-ended (All time has neither).
    public record RevenueRange(RevenuePreset Preset, DateTime? From, DateTime? To);

    // How the revenue graph groups payments over time.
    public enum RevenueGrouping
    {
        Weekly,
        Monthly
    }

    // One period on the revenue graph: a Monday-to-Sunday week or a calendar
    // month, with the net revenue paid for classes in it (zero for a
    // period with nothing paid).
    public record RevenuePoint(DateTime PeriodStart, string Label, string LongLabel, decimal Amount);

    // One payment, flattened with what the report groups by. ClassDate is
    // the date of the class the payment is for, which is the date the
    // report uses for every payment (EFT and cash have no separate paid-on
    // date).
    public record RevenuePaymentRow(
        int PaymentId,
        int ClientId,
        string ClientName,
        string ClientEmail,
        decimal Amount,
        string Status,
        string Method,
        string PaymentType,
        string LocationName,
        DateTime ClassDate);

    // The admin Revenue page. Built by RevenueService.Build.
    public class RevenueReport
    {
        public RevenueRange Range { get; set; } = new(RevenuePreset.AllTime, null, null);

        // Paid payments only. Refunded ones are counted separately below,
        // and Refund due, Pending and declined ones aren't revenue at all.
        public decimal NetRevenue { get; set; }
        public int PaidCount { get; set; }
        public decimal AveragePerPayment { get; set; }
        public decimal TotalRefunded { get; set; }
        public int RefundedCount { get; set; }

        public IReadOnlyList<RevenueBreakdownRow> ByMonth { get; set; } = Array.Empty<RevenueBreakdownRow>();

        // The graph: net revenue per week or month across the range, empty
        // when nothing was paid in it.
        public RevenueGrouping Grouping { get; set; } = RevenueGrouping.Weekly;
        public IReadOnlyList<RevenuePoint> Series { get; set; } = Array.Empty<RevenuePoint>();
        public IReadOnlyList<RevenueBreakdownRow> ByMethod { get; set; } = Array.Empty<RevenueBreakdownRow>();
        public IReadOnlyList<RevenueBreakdownRow> ByPaymentType { get; set; } = Array.Empty<RevenueBreakdownRow>();
        public IReadOnlyList<RevenueBreakdownRow> ByVenue { get; set; } = Array.Empty<RevenueBreakdownRow>();

        // Every client with at least one paid payment in the range, biggest
        // spender first; TopClients is the first three of these.
        public IReadOnlyList<ClientSpendRow> Clients { get; set; } = Array.Empty<ClientSpendRow>();
        public IReadOnlyList<ClientSpendRow> TopClients { get; set; } = Array.Empty<ClientSpendRow>();
    }

    public class RevenueBreakdownRow
    {
        public string Label { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }

        // Fraction of net revenue, 0 to 1.
        public decimal Share { get; set; }
    }

    public class ClientSpendRow
    {
        public int ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int PaymentsCount { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime LastPaymentDate { get; set; }
    }
}
