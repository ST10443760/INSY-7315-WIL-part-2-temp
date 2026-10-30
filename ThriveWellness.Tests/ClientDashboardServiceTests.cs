using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

// ClientDashboardService.Build holds every dashboard rule (upcoming versus
// past, the tiles' totals), so it's tested directly with no database.
public class ClientDashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0);

    private static ClientBookingViewModel Booking(int id, DateTime date, TimeSpan time, string status = "Confirmed",
        string? paymentStatus = PaymentStatuses.Confirmed, string method = PaymentMethods.Card, decimal amount = 120m) => new()
    {
        BookingId = id,
        SessionType = "group",
        SessionDate = date,
        SessionTime = time,
        Status = status,
        PaymentId = id,
        PaymentMethod = method,
        PaymentStatus = paymentStatus,
        Amount = amount
    };

    private static ClientPaymentViewModel Payment(int id, string status, decimal amount = 120m, DateTime? sessionDate = null) => new()
    {
        PaymentId = id,
        BookingId = id,
        SessionDate = sessionDate ?? Now.Date,
        Amount = amount,
        Method = PaymentMethods.Card,
        Status = status
    };

    private static ClientDashboardViewModel Build(
        IEnumerable<ClientBookingViewModel>? bookings = null,
        IEnumerable<ClientPaymentViewModel>? payments = null,
        IEnumerable<ClientWaitlistViewModel>? waitlist = null) =>
        ClientDashboardService.Build("Thandi", bookings ?? [], payments ?? [], waitlist ?? [], Now);

    [Fact]
    public void Build_SplitsUpcomingAndPastByStartTimeNotJustTheDate()
    {
        var laterToday = Booking(1, Now.Date, new TimeSpan(18, 0, 0));
        var earlierToday = Booking(2, Now.Date, new TimeSpan(8, 0, 0));
        var tomorrow = Booking(3, Now.Date.AddDays(1), new TimeSpan(9, 0, 0));
        var lastWeek = Booking(4, Now.Date.AddDays(-7), new TimeSpan(9, 0, 0));

        var dashboard = Build(new[] { tomorrow, lastWeek, laterToday, earlierToday });

        Assert.Equal(new[] { 1, 3 }, dashboard.Upcoming.Select(b => b.BookingId));
        Assert.Equal(new[] { 2, 4 }, dashboard.Past.Select(b => b.BookingId));
    }

    [Fact]
    public void Build_ClassStartingRightNow_IsPast()
    {
        var dashboard = Build(new[] { Booking(1, Now.Date, Now.TimeOfDay) });

        Assert.Empty(dashboard.Upcoming);
        Assert.Single(dashboard.Past);
    }

    [Fact]
    public void Build_UpcomingCount_LeavesOutCancelledBookings()
    {
        var dashboard = Build(new[]
        {
            Booking(1, Now.Date.AddDays(1), TimeSpan.FromHours(9)),
            Booking(2, Now.Date.AddDays(2), TimeSpan.FromHours(9), status: "Cancelled"),
            Booking(3, Now.Date.AddDays(3), TimeSpan.FromHours(9), status: "Awaiting Payment", paymentStatus: PaymentStatuses.Pending)
        });

        Assert.Equal(2, dashboard.UpcomingCount);
        Assert.Equal(3, dashboard.Upcoming.Count);
    }

    [Fact]
    public void Build_Attended_CountsOnlyPastPaidNotCancelledClasses()
    {
        var dashboard = Build(new[]
        {
            Booking(1, Now.Date.AddDays(-1), TimeSpan.FromHours(9)),
            Booking(2, Now.Date.AddDays(-2), TimeSpan.FromHours(9)),
            Booking(3, Now.Date.AddDays(-3), TimeSpan.FromHours(9), status: "Cancelled"),
            Booking(4, Now.Date.AddDays(-4), TimeSpan.FromHours(9), status: "Awaiting Payment", paymentStatus: PaymentStatuses.Pending),
            Booking(5, Now.Date.AddDays(1), TimeSpan.FromHours(9))
        });

        Assert.Equal(2, dashboard.AttendedCount);
    }

    [Fact]
    public void Build_TotalSpent_AddsUpConfirmedPaymentsOnly()
    {
        var dashboard = Build(payments: new[]
        {
            Payment(1, PaymentStatuses.Confirmed, 120m),
            Payment(2, PaymentStatuses.Confirmed, 450m),
            Payment(3, PaymentStatuses.Pending, 120m),
            Payment(4, "Refunded", 120m),
            Payment(5, "Refund due", 450m)
        });

        Assert.Equal(570m, dashboard.TotalSpent);
    }

    [Fact]
    public void Build_PaymentsList_LeavesOutUnpaidOnesAndShowsNewestFirst()
    {
        var dashboard = Build(payments: new[]
        {
            Payment(1, PaymentStatuses.Confirmed, sessionDate: Now.Date.AddDays(-5)),
            Payment(2, PaymentStatuses.Pending, sessionDate: Now.Date.AddDays(1)),
            Payment(3, PaymentStatuses.Confirmed, sessionDate: Now.Date.AddDays(2)),
            Payment(4, "Refunded", sessionDate: Now.Date)
        });

        Assert.Equal(new[] { 3, 4, 1 }, dashboard.Payments.Select(p => p.PaymentId));
    }

    [Fact]
    public void Build_Waitlist_ShowsOnlySessionsStillToComeInDateOrder()
    {
        var dashboard = Build(waitlist: new[]
        {
            new ClientWaitlistViewModel { SessionId = 1, SessionDate = Now.Date.AddDays(3), SessionTime = TimeSpan.FromHours(9), Position = 2 },
            new ClientWaitlistViewModel { SessionId = 2, SessionDate = Now.Date, SessionTime = TimeSpan.FromHours(8), Position = 1 },
            new ClientWaitlistViewModel { SessionId = 3, SessionDate = Now.Date.AddDays(1), SessionTime = TimeSpan.FromHours(9), Position = 4 }
        });

        Assert.Equal(new[] { 3, 1 }, dashboard.Waitlist.Select(w => w.SessionId));
    }

    [Fact]
    public void Build_EmptyClient_HasZeroTotalsAndEmptySections()
    {
        var dashboard = Build();

        Assert.Equal("Thandi", dashboard.FirstName);
        Assert.Equal(0, dashboard.UpcomingCount);
        Assert.Equal(0, dashboard.AttendedCount);
        Assert.Equal(0m, dashboard.TotalSpent);
        Assert.Empty(dashboard.Upcoming);
        Assert.Empty(dashboard.Past);
        Assert.Empty(dashboard.Payments);
        Assert.Empty(dashboard.Waitlist);
    }

    [Theory]
    [InlineData("Confirmed", PaymentStatuses.Confirmed, "Paid")]
    [InlineData("Awaiting Payment", PaymentStatuses.Pending, "Awaiting payment")]
    [InlineData("Cancelled", PaymentStatuses.Pending, "Cancelled")]
    [InlineData("Cancelled", PaymentStatuses.Confirmed, "Cancelled")]
    public void StatusLabel_MatchesTheBookingAndPayment(string bookingStatus, string paymentStatus, string expected)
    {
        var booking = Booking(1, Now.Date, TimeSpan.Zero, status: bookingStatus, paymentStatus: paymentStatus);

        Assert.Equal(expected, booking.StatusLabel);
    }

    [Fact]
    public void Build_Attended_LeavesOutNoShows()
    {
        var noShow = Booking(1, Now.Date.AddDays(-1), TimeSpan.FromHours(9));
        noShow.NoShow = true;

        var dashboard = Build(new[] { noShow, Booking(2, Now.Date.AddDays(-2), TimeSpan.FromHours(9)) });

        Assert.Equal(1, dashboard.AttendedCount);
        Assert.Equal("No-show", noShow.StatusLabel);
    }

    [Fact]
    public void ConfirmedBookingWithoutAPaymentRow_CountsAsPaid()
    {
        var booking = Booking(1, Now.Date.AddDays(-1), TimeSpan.FromHours(9), paymentStatus: null);

        Assert.Equal("Paid", booking.StatusLabel);
        Assert.Equal(1, Build(new[] { booking }).AttendedCount);
    }

    [Fact]
    public void CanPayByCard_OnlyForUnpaidNotCancelledCardBookings()
    {
        Assert.True(Booking(1, Now.Date, TimeSpan.Zero, "Awaiting Payment", PaymentStatuses.Pending).CanPayByCard);
        Assert.False(Booking(2, Now.Date, TimeSpan.Zero, "Awaiting Payment", PaymentStatuses.Pending, PaymentMethods.Eft).CanPayByCard);
        Assert.False(Booking(3, Now.Date, TimeSpan.Zero, "Confirmed", PaymentStatuses.Confirmed).CanPayByCard);
        Assert.False(Booking(4, Now.Date, TimeSpan.Zero, "Cancelled", PaymentStatuses.Pending).CanPayByCard);
    }
}
