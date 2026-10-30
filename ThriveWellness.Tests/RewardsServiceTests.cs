using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class RewardsServiceTests
{
    private const int ClientId = 1;

    // Wednesday 4 November 2026, midday.
    private class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTime Today = new(2026, 11, 4);

    private readonly ApplicationDbContext _context;
    private int _nextId = 1;

    public RewardsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        // Creates the seeded badges and both studios (locations 1 and 2).
        _context.Database.EnsureCreated();
        _context.Clients.Add(new Client { ClientId = ClientId, FullName = "Jane Doe", Email = "jane@example.com" });
        _context.SaveChanges();
    }

    private RewardsService CreateService() => new(_context, new FixedClock(), NullLogger<RewardsService>.Instance);

    // A booking for this client at a new session, paid unless said otherwise.
    private void AddClass(DateTime date, TimeSpan? time = null, int locationId = 1,
        string bookingStatus = "Confirmed", string paymentStatus = PaymentStatuses.Confirmed, bool noShow = false)
    {
        var id = _nextId++;
        _context.Sessions.Add(new Session { SessionId = id, LocationId = locationId, SessionType = "group", Date = date, Time = time ?? new TimeSpan(10, 0, 0), Capacity = 10, IsOpen = true });
        _context.Bookings.Add(new Booking { BookingId = id, ClientId = ClientId, SessionId = id, Status = bookingStatus, NoShow = noShow, CancellationToken = $"t{id}" });
        _context.Payments.Add(new Payment { PaymentId = id, BookingId = id, Method = PaymentMethods.Eft, Amount = 120m, Status = paymentStatus, PaymentType = "per-class" });
        _context.SaveChanges();
    }

    private List<string> AwardedCodes() =>
        (from cb in _context.ClientBadges
         join b in _context.Badges on cb.BadgeId equals b.BadgeId
         where cb.ClientId == ClientId
         select b.Code).ToList();

    [Fact]
    public async Task EvaluateAsync_NoAttendedClasses_AwardsNothing()
    {
        AddClass(Today.AddDays(2));
        AddClass(Today.AddDays(-2), paymentStatus: PaymentStatuses.Pending);

        var awarded = await CreateService().EvaluateAsync(ClientId);

        Assert.Empty(awarded);
        Assert.Empty(AwardedCodes());
    }

    [Fact]
    public async Task EvaluateAsync_FirstAttendedClass_AwardsFirstClassOnce()
    {
        AddClass(Today.AddDays(-1));
        var service = CreateService();

        var first = await service.EvaluateAsync(ClientId);
        var second = await service.EvaluateAsync(ClientId);

        Assert.Equal(new[] { BadgeCodes.FirstClass }, first);
        Assert.Empty(second);
        Assert.Single(AwardedCodes());
    }

    [Fact]
    public async Task EvaluateAsync_SeveralEarnedAtOnce_AwardsThemAllTogether()
    {
        // Five classes in five consecutive weeks.
        for (var week = 0; week < 5; week++)
        {
            AddClass(Today.AddDays(-1 - 7 * week));
        }

        var awarded = await CreateService().EvaluateAsync(ClientId);

        Assert.Equal(
            new[] { BadgeCodes.FirstClass, BadgeCodes.GettingGoing, BadgeCodes.ThreeWeekStreak }.OrderBy(c => c),
            awarded.OrderBy(c => c));
    }

    [Fact]
    public async Task EvaluateAsync_ClassesAtBothStudios_AwardsBothStudios()
    {
        AddClass(Today.AddDays(-3), locationId: 1);
        AddClass(Today.AddDays(-2), locationId: 1);
        var service = CreateService();

        Assert.DoesNotContain(BadgeCodes.BothStudios, await service.EvaluateAsync(ClientId));

        AddClass(Today.AddDays(-1), locationId: 2);
        Assert.Contains(BadgeCodes.BothStudios, await service.EvaluateAsync(ClientId));
    }

    [Fact]
    public async Task EvaluateAsync_BothStudios_NeedsTheSecondStudioClassToBeAttended()
    {
        AddClass(Today.AddDays(-3), locationId: 1);
        AddClass(Today.AddDays(-2), locationId: 2, noShow: true);
        AddClass(Today.AddDays(5), locationId: 2);

        Assert.DoesNotContain(BadgeCodes.BothStudios, await CreateService().EvaluateAsync(ClientId));
    }

    [Fact]
    public async Task EvaluateAsync_ThreeClassesBeforeNine_AwardsEarlyBird()
    {
        AddClass(Today.AddDays(-10), new TimeSpan(7, 0, 0));
        AddClass(Today.AddDays(-9), new TimeSpan(8, 30, 0));
        AddClass(Today.AddDays(-8), new TimeSpan(9, 0, 0));
        var service = CreateService();

        Assert.DoesNotContain(BadgeCodes.EarlyBird, await service.EvaluateAsync(ClientId));

        AddClass(Today.AddDays(-7), new TimeSpan(8, 59, 0));
        Assert.Contains(BadgeCodes.EarlyBird, await service.EvaluateAsync(ClientId));
    }

    [Fact]
    public async Task EvaluateAsync_EarlyBird_IgnoresCancelledAndNoShowClasses()
    {
        AddClass(Today.AddDays(-10), new TimeSpan(7, 0, 0));
        AddClass(Today.AddDays(-9), new TimeSpan(7, 0, 0), bookingStatus: "Cancelled");
        AddClass(Today.AddDays(-8), new TimeSpan(7, 0, 0), noShow: true);
        AddClass(Today.AddDays(-7), new TimeSpan(7, 0, 0));

        Assert.DoesNotContain(BadgeCodes.EarlyBird, await CreateService().EvaluateAsync(ClientId));
    }

    [Fact]
    public async Task EvaluateAsync_BadgesAreNeverTakenBack()
    {
        AddClass(Today.AddDays(-1));
        var service = CreateService();
        await service.EvaluateAsync(ClientId);

        var booking = _context.Bookings.Single();
        booking.NoShow = true;
        _context.SaveChanges();
        await service.EvaluateAsync(ClientId);

        Assert.Equal(new[] { BadgeCodes.FirstClass }, AwardedCodes());
    }

    [Fact]
    public async Task GetRewardsAsync_ShowsStreakNextBadgeAndEveryBadge()
    {
        AddClass(Today.AddDays(-1));
        AddClass(Today.AddDays(-8));
        var service = CreateService();
        await service.EvaluateAsync(ClientId);

        var rewards = await service.GetRewardsAsync(ClientId);

        Assert.Equal(2, rewards.AttendedCount);
        Assert.Equal(2, rewards.CurrentStreak);
        Assert.Equal(2, rewards.LongestStreak);
        Assert.Equal("3 more classes to Getting Going", rewards.NextBadgeText);
        Assert.Equal(8, rewards.Badges.Count);
        var firstClass = rewards.Badges.Single(b => b.Code == BadgeCodes.FirstClass);
        Assert.True(firstClass.Earned);
        Assert.True(firstClass.IsNew);
        Assert.NotNull(firstClass.AwardedAt);
        Assert.All(rewards.Badges.Where(b => b.Code != BadgeCodes.FirstClass), b => Assert.False(b.Earned));
    }

    [Fact]
    public async Task GetRewardsAsync_BadgeAwardedOverAWeekAgo_IsNotNew()
    {
        AddClass(Today.AddDays(-20));
        _context.ClientBadges.Add(new ClientBadge { ClientId = ClientId, BadgeId = 1, AwardedAt = DateTime.UtcNow.AddDays(-8) });
        _context.SaveChanges();

        var rewards = await CreateService().GetRewardsAsync(ClientId);

        Assert.False(rewards.Badges.Single(b => b.Code == BadgeCodes.FirstClass).IsNew);
    }

    [Fact]
    public async Task GetRewardsAsync_NewClient_HasNothingAttended()
    {
        var rewards = await CreateService().GetRewardsAsync(ClientId);

        Assert.False(rewards.HasAttended);
        Assert.Equal(0, rewards.EarnedCount);
        Assert.Equal("1 more class to First Class", rewards.NextBadgeText);
    }

    [Fact]
    public async Task NoShow_IsNotAttendedAndBreaksTheStreak()
    {
        AddClass(Today.AddDays(-1));
        AddClass(Today.AddDays(-8), noShow: true);
        AddClass(Today.AddDays(-15));

        var rewards = await CreateService().GetRewardsAsync(ClientId);

        Assert.Equal(2, rewards.AttendedCount);
        Assert.Equal(1, rewards.CurrentStreak);
        Assert.Equal(1, rewards.LongestStreak);
    }

    [Fact]
    public async Task UndoingANoShow_CountsTheClassAgain()
    {
        AddClass(Today.AddDays(-1));
        AddClass(Today.AddDays(-8), noShow: true);
        var service = CreateService();

        var booking = _context.Bookings.Single(b => b.NoShow);
        booking.NoShow = false;
        _context.SaveChanges();
        var rewards = await service.GetRewardsAsync(ClientId);

        Assert.Equal(2, rewards.AttendedCount);
        Assert.Equal(2, rewards.CurrentStreak);
    }

    [Fact]
    public async Task GetSummariesAsync_CountsAttendedStreakAndBadgesPerClient()
    {
        AddClass(Today.AddDays(-1));
        AddClass(Today.AddDays(-8));
        AddClass(Today.AddDays(-9), noShow: true);
        var service = CreateService();
        await service.EvaluateAsync(ClientId);

        var summaries = await service.GetSummariesAsync();

        Assert.Equal(new ClientRewardsSummary(2, 2, 1), summaries[ClientId]);
    }

    [Fact]
    public void NextBadgeText_AllClassBadgesEarned_PointsAtTheNextStreak()
    {
        var stats = new RewardsStats { AttendedCount = 30, CurrentStreak = 2 };
        var earned = new HashSet<string> { BadgeCodes.FirstClass, BadgeCodes.GettingGoing, BadgeCodes.Committed, BadgeCodes.Dedicated };
        var names = new Dictionary<string, string> { [BadgeCodes.ThreeWeekStreak] = "Three Week Streak", [BadgeCodes.FirstClass] = "First Class" };

        Assert.Equal("1 more week in a row to Three Week Streak", BadgeRules.NextBadgeText(stats, earned, names));
    }
}
