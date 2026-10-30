using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class ScheduledNotificationServiceTests
{
    private readonly Mock<INotificationService> _notificationService = new();

    private static ApplicationDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private ScheduledNotificationService CreateService(ApplicationDbContext context)
    {
        return new ScheduledNotificationService(context, _notificationService.Object, NullLogger<ScheduledNotificationService>.Instance);
    }

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);

    private static void AddSession(ApplicationDbContext context, int sessionId, DateTime date)
    {
        context.Sessions.Add(new Session
        {
            SessionId = sessionId,
            LocationId = 1,
            SessionType = "group",
            Date = date,
            Time = new TimeSpan(9, 0, 0),
            Capacity = 10,
            IsOpen = true
        });
    }

    private static void AddBooking(ApplicationDbContext context, int bookingId, int sessionId, string status = "Confirmed")
    {
        context.Bookings.Add(new Booking
        {
            BookingId = bookingId,
            ClientId = 1,
            SessionId = sessionId,
            Status = status,
            CancellationToken = $"token-{bookingId}"
        });
    }

    private static void AddReminderLog(ApplicationDbContext context, int bookingId)
    {
        context.Notifications.Add(new Notification { BookingId = bookingId, Type = "Reminder", SentAt = DateTime.UtcNow.AddHours(-2) });
    }

    [Fact]
    public async Task SendDueRemindersAsync_CountsSentAndAlreadyRemindedBookings()
    {
        using var context = NewInMemoryContext();
        AddSession(context, 1, Tomorrow);
        AddSession(context, 2, Tomorrow.AddDays(3));
        AddBooking(context, 10, 1);
        AddBooking(context, 11, 1);
        AddBooking(context, 12, 1);
        AddReminderLog(context, 12);
        // Not counted at all: unpaid, cancelled, or not tomorrow.
        AddBooking(context, 13, 1, "Awaiting Payment");
        AddBooking(context, 14, 1, "Cancelled");
        AddBooking(context, 15, 2);
        await context.SaveChangesAsync();

        var result = await CreateService(context).SendDueRemindersAsync();

        Assert.Equal(2, result.Sent);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Failed);
        _notificationService.Verify(n => n.SendReminderEmailAsync(It.Is<Booking>(b => b.BookingId == 10)), Times.Once);
        _notificationService.Verify(n => n.SendReminderEmailAsync(It.Is<Booking>(b => b.BookingId == 11)), Times.Once);
        _notificationService.Verify(n => n.SendReminderEmailAsync(It.Is<Booking>(b => b.BookingId == 12)), Times.Never);
    }

    [Fact]
    public async Task SendDueRemindersAsync_OneSendFails_CountsItAndStillSendsTheRest()
    {
        using var context = NewInMemoryContext();
        AddSession(context, 1, Tomorrow);
        AddBooking(context, 10, 1);
        AddBooking(context, 11, 1);
        AddBooking(context, 12, 1);
        await context.SaveChangesAsync();

        _notificationService
            .Setup(n => n.SendReminderEmailAsync(It.Is<Booking>(b => b.BookingId == 11)))
            .ThrowsAsync(new InvalidOperationException("SendGrid returned 401"));

        var result = await CreateService(context).SendDueRemindersAsync();

        Assert.Equal(2, result.Sent);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task SendDueRemindersAsync_NothingDue_ReturnsAllZeros()
    {
        using var context = NewInMemoryContext();

        var result = await CreateService(context).SendDueRemindersAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        _notificationService.Verify(n => n.SendReminderEmailAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task SendDueRemindersAsync_RunTwice_SecondRunSkipsEveryoneAlreadyReminded()
    {
        // The real NotificationService logs a Reminder row on every send;
        // the mock stands in for that so the second run can see it.
        using var context = NewInMemoryContext();
        AddSession(context, 1, Tomorrow);
        AddBooking(context, 10, 1);
        AddBooking(context, 11, 1);
        await context.SaveChangesAsync();

        _notificationService
            .Setup(n => n.SendReminderEmailAsync(It.IsAny<Booking>()))
            .Returns<Booking>(async b =>
            {
                AddReminderLog(context, b.BookingId);
                await context.SaveChangesAsync();
            });

        var service = CreateService(context);
        var first = await service.SendDueRemindersAsync();
        var second = await service.SendDueRemindersAsync();

        Assert.Equal(2, first.Sent);
        Assert.Equal(0, second.Sent);
        Assert.Equal(2, second.Skipped);
    }

    [Theory]
    [InlineData(3, 1, 0, "Sent 3 reminders, skipped 1")]
    [InlineData(1, 0, 0, "Sent 1 reminder, skipped 0")]
    [InlineData(0, 0, 0, "Sent 0 reminders, skipped 0")]
    [InlineData(2, 0, 1, "Sent 2 reminders, skipped 0, 1 failed")]
    public void ToSummary_ReadsNaturally(int sent, int skipped, int failed, string expected)
    {
        var result = new ReminderRunResult { Sent = sent, Skipped = skipped, Failed = failed };

        Assert.Equal(expected, result.ToSummary());
    }
}
