using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class RevenueServiceTests
{
    private static readonly RevenueRange AllTime = new(RevenuePreset.AllTime, null, null);
    private int _nextId = 1;

    private RevenuePaymentRow Row(int clientId, decimal amount, DateTime classDate, string status = PaymentStatuses.Confirmed,
        string method = PaymentMethods.Eft, string type = "per-class", string location = "Sunninghill", string? name = null) =>
        new(_nextId++, clientId, name ?? $"Client {clientId}", $"c{clientId}@example.com", amount, status, method, type, location, classDate);

    [Fact]
    public void Build_NetRevenueIsPaidOnly_RefundsShownSeparately()
    {
        var rows = new[]
        {
            Row(1, 120m, new DateTime(2026, 10, 1)),
            Row(1, 450m, new DateTime(2026, 10, 2)),
            Row(2, 120m, new DateTime(2026, 10, 3), PaymentStatuses.Refunded),
            Row(2, 120m, new DateTime(2026, 10, 4), PaymentStatuses.RefundDue),
            Row(3, 120m, new DateTime(2026, 10, 5), PaymentStatuses.Pending)
        };

        var report = RevenueService.Build(rows, AllTime);

        Assert.Equal(570m, report.NetRevenue);
        Assert.Equal(2, report.PaidCount);
        Assert.Equal(285m, report.AveragePerPayment);
        Assert.Equal(120m, report.TotalRefunded);
        Assert.Equal(1, report.RefundedCount);
        Assert.Equal(new[] { 1 }, report.Clients.Select(c => c.ClientId));
    }

    [Fact]
    public void Build_DateRange_IncludesBothEndsAndNothingOutside()
    {
        var rows = new[]
        {
            Row(1, 100m, new DateTime(2026, 9, 30)),
            Row(1, 200m, new DateTime(2026, 10, 1)),
            Row(1, 300m, new DateTime(2026, 10, 31)),
            Row(1, 400m, new DateTime(2026, 11, 1)),
            Row(2, 50m, new DateTime(2026, 10, 15), PaymentStatuses.Refunded)
        };

        var report = RevenueService.Build(rows, new RevenueRange(RevenuePreset.Custom, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));

        Assert.Equal(500m, report.NetRevenue);
        Assert.Equal(2, report.PaidCount);
        Assert.Equal(50m, report.TotalRefunded);
    }

    [Fact]
    public void Build_ByMonth_GroupsByClassMonthAndFillsEmptyMonths()
    {
        var rows = new[]
        {
            Row(1, 120m, new DateTime(2026, 8, 31)),
            Row(1, 120m, new DateTime(2026, 8, 1)),
            Row(2, 450m, new DateTime(2026, 10, 15))
        };

        var report = RevenueService.Build(rows, AllTime);

        Assert.Equal(new[] { "Aug 2026", "Sep 2026", "Oct 2026" }, report.ByMonth.Select(m => m.Label));
        Assert.Equal(new[] { 240m, 0m, 450m }, report.ByMonth.Select(m => m.Amount));
        Assert.Equal(new[] { 2, 0, 1 }, report.ByMonth.Select(m => m.Count));
        Assert.Equal(1m, Math.Round(report.ByMonth.Sum(m => m.Share), 10));
    }

    [Fact]
    public void Build_Breakdowns_ByMethodTypeAndVenueWithShares()
    {
        var rows = new[]
        {
            Row(1, 450m, new DateTime(2026, 10, 1), method: PaymentMethods.Card, type: "monthly", location: "Kyalami"),
            Row(2, 120m, new DateTime(2026, 10, 2), method: PaymentMethods.Eft),
            Row(3, 30m, new DateTime(2026, 10, 3), method: PaymentMethods.Cash)
        };

        var report = RevenueService.Build(rows, AllTime);

        Assert.Equal(new[] { "Card", "EFT", "Cash" }, report.ByMethod.Select(r => r.Label));
        Assert.Equal(0.75m, report.ByMethod[0].Share);
        Assert.Equal(new[] { "Monthly pass", "Per class" }, report.ByPaymentType.Select(r => r.Label));
        Assert.Equal(new[] { 450m, 150m }, report.ByPaymentType.Select(r => r.Amount));
        Assert.Equal(new[] { "Kyalami", "Sunninghill" }, report.ByVenue.Select(r => r.Label));
        Assert.Equal(new[] { 1, 2 }, report.ByVenue.Select(r => r.Count));
    }

    [Fact]
    public void Build_TopClients_TiesBrokenByPaymentCountThenName()
    {
        var rows = new[]
        {
            Row(1, 450m, new DateTime(2026, 10, 1), name: "Zanele"),
            Row(2, 225m, new DateTime(2026, 10, 1), name: "Bongi"),
            Row(2, 225m, new DateTime(2026, 10, 2), name: "Bongi"),
            Row(3, 450m, new DateTime(2026, 10, 1), name: "Anna"),
            Row(4, 120m, new DateTime(2026, 10, 1), name: "Carla")
        };

        var report = RevenueService.Build(rows, AllTime);

        Assert.Equal(new[] { "Bongi", "Anna", "Zanele" }, report.TopClients.Select(c => c.Name));
        Assert.Equal(4, report.Clients.Count);
        var bongi = report.Clients[0];
        Assert.Equal(2, bongi.PaymentsCount);
        Assert.Equal(450m, bongi.TotalSpent);
        Assert.Equal(new DateTime(2026, 10, 2), bongi.LastPaymentDate);
    }

    [Fact]
    public void Build_NoPayments_GivesZerosAndEmptyLists()
    {
        var report = RevenueService.Build(Array.Empty<RevenuePaymentRow>(), AllTime);

        Assert.Equal(0m, report.NetRevenue);
        Assert.Equal(0, report.PaidCount);
        Assert.Equal(0m, report.AveragePerPayment);
        Assert.Empty(report.ByMonth);
        Assert.Empty(report.ByMethod);
        Assert.Empty(report.Clients);
        Assert.Empty(report.TopClients);
    }

    [Fact]
    public void Build_ClientWithOnlyUnpaidOrRefundedPayments_IsNotListed()
    {
        var rows = new[]
        {
            Row(1, 120m, new DateTime(2026, 10, 1), PaymentStatuses.Refunded),
            Row(2, 120m, new DateTime(2026, 10, 1))
        };

        var report = RevenueService.Build(rows, AllTime);

        Assert.Equal(new[] { 2 }, report.Clients.Select(c => c.ClientId));
    }

    [Theory]
    [InlineData("month", "2026-11-01", "2026-11-04", RevenuePreset.ThisMonth)]
    [InlineData("30days", "2026-10-06", "2026-11-04", RevenuePreset.Last30Days)]
    [InlineData("year", "2026-01-01", "2026-11-04", RevenuePreset.ThisYear)]
    public void ResolveRange_Presets(string preset, string from, string to, RevenuePreset expected)
    {
        var range = RevenueService.ResolveRange(preset, null, null, new DateTime(2026, 11, 4));

        Assert.Equal(expected, range.Preset);
        Assert.Equal(DateTime.Parse(from), range.From);
        Assert.Equal(DateTime.Parse(to), range.To);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("all")]
    [InlineData("nonsense")]
    public void ResolveRange_DefaultsToAllTime(string? preset)
    {
        var range = RevenueService.ResolveRange(preset, null, null, new DateTime(2026, 11, 4));

        Assert.Equal(RevenuePreset.AllTime, range.Preset);
        Assert.Null(range.From);
        Assert.Null(range.To);
    }

    [Fact]
    public void ResolveRange_CustomDatesGivenBackwards_AreSwapped()
    {
        var range = RevenueService.ResolveRange("month", new DateTime(2026, 10, 31), new DateTime(2026, 10, 1), new DateTime(2026, 11, 4));

        Assert.Equal(RevenuePreset.Custom, range.Preset);
        Assert.Equal(new DateTime(2026, 10, 1), range.From);
        Assert.Equal(new DateTime(2026, 10, 31), range.To);
    }

    [Fact]
    public async Task GetReportAsync_ReadsPaidAndRefundedPaymentsFromTheDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        context.Clients.Add(new Client { ClientId = 1, FullName = "Jane Doe", Email = "jane@example.com" });
        context.Clients.Add(new Client { ClientId = 2, FullName = "No Payments", Email = "none@example.com" });
        var statuses = new[] { PaymentStatuses.Confirmed, PaymentStatuses.Refunded, PaymentStatuses.Pending, PaymentStatuses.RefundDue };
        for (var i = 1; i <= statuses.Length; i++)
        {
            context.Sessions.Add(new Session { SessionId = i, LocationId = 1, SessionType = "group", Date = new DateTime(2026, 10, i), Time = TimeSpan.FromHours(9), Capacity = 10 });
            context.Bookings.Add(new Booking { BookingId = i, ClientId = 1, SessionId = i, Status = "Confirmed", CancellationToken = $"t{i}" });
            context.Payments.Add(new Payment { PaymentId = i, BookingId = i, Method = PaymentMethods.Card, Amount = 100m * i, Status = statuses[i - 1], PaymentType = "per-class" });
        }
        context.SaveChanges();

        var report = await new RevenueService(context, TimeProvider.System).GetReportAsync(AllTime);

        Assert.Equal(100m, report.NetRevenue);
        Assert.Equal(200m, report.TotalRefunded);
        Assert.Equal("The Hub, Little Village", report.ByVenue.Single().Label);
        Assert.Equal("Jane Doe", report.Clients.Single().Name);
    }
}
