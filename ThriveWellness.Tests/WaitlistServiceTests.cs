using Moq;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class WaitlistServiceTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();

    private WaitlistService CreateService() => new(
        _waitlistRepository.Object,
        _bookingRepository.Object,
        _sessionRepository.Object,
        _notificationService.Object);

    private static Session OpenSession(int capacity = 1) => new()
    {
        SessionId = 1,
        LocationId = 1,
        SessionType = "group",
        Date = DateTime.Today.AddDays(1),
        Time = new TimeSpan(9, 0, 0),
        Capacity = capacity,
        IsOpen = true
    };

    [Fact]
    public async Task JoinWaitlistAsync_EmptyWaitlist_AssignsPositionOne()
    {
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Waitlist>());

        var service = CreateService();
        var entry = await service.JoinWaitlistAsync(clientId: 5, sessionId: 1);

        Assert.Equal(1, entry.Position);
        _waitlistRepository.Verify(r => r.AddAsync(It.Is<Waitlist>(w => w.Position == 1 && w.ClientId == 5)), Times.Once);
    }

    [Fact]
    public async Task JoinWaitlistAsync_ExistingEntries_AssignsNextPosition()
    {
        var existing = new[]
        {
            new Waitlist { WaitlistId = 1, SessionId = 1, ClientId = 10, Position = 1 },
            new Waitlist { WaitlistId = 2, SessionId = 1, ClientId = 11, Position = 2 }
        };
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(existing);

        var service = CreateService();
        var entry = await service.JoinWaitlistAsync(clientId: 12, sessionId: 1);

        Assert.Equal(3, entry.Position);
        _waitlistRepository.Verify(r => r.AddAsync(It.Is<Waitlist>(w => w.Position == 3)), Times.Once);
    }

    [Fact]
    public async Task PromoteNextInLineAsync_RoomAvailable_RemovesEntryAndCreatesRealBooking()
    {
        var next = new Waitlist { WaitlistId = 1, SessionId = 1, ClientId = 7, Position = 1 };
        _waitlistRepository.Setup(r => r.GetNextInLineAsync(1)).ReturnsAsync(next);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 2));
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Waitlist>());
        Booking? created = null;
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => created = b)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PromoteNextInLineAsync(1);

        _waitlistRepository.Verify(r => r.RemoveAsync(1), Times.Once);
        Assert.NotNull(created);
        Assert.Equal(7, created!.ClientId);
        Assert.Equal(1, created.SessionId);
        _notificationService.Verify(n => n.SendWaitlistNotificationAsync(created), Times.Once);
    }

    [Fact]
    public async Task PromoteNextInLineAsync_RemovingEntry_ShiftsLaterPositionsDown()
    {
        var next = new Waitlist { WaitlistId = 1, SessionId = 1, ClientId = 7, Position = 1 };
        var laterOne = new Waitlist { WaitlistId = 2, SessionId = 1, ClientId = 8, Position = 2 };
        var laterTwo = new Waitlist { WaitlistId = 3, SessionId = 1, ClientId = 9, Position = 3 };
        _waitlistRepository.Setup(r => r.GetNextInLineAsync(1)).ReturnsAsync(next);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 5));
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        // Queried again after the removal, so it no longer includes "next".
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(new[] { laterOne, laterTwo });
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PromoteNextInLineAsync(1);

        Assert.Equal(1, laterOne.Position);
        Assert.Equal(2, laterTwo.Position);
        _waitlistRepository.Verify(r => r.UpdateAsync(laterOne), Times.Once);
        _waitlistRepository.Verify(r => r.UpdateAsync(laterTwo), Times.Once);
    }

    [Fact]
    public async Task PromoteNextInLineAsync_SessionStillAtCapacity_DoesNotPromote()
    {
        // The bug fixed earlier: MarkAsOpenAsync used to promote blindly
        // without re-checking capacity.
        var next = new Waitlist { WaitlistId = 1, SessionId = 1, ClientId = 7, Position = 1 };
        _waitlistRepository.Setup(r => r.GetNextInLineAsync(1)).ReturnsAsync(next);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 1));
        _bookingRepository
            .Setup(r => r.GetBookingsBySessionAsync(1))
            .ReturnsAsync(new[] { new Booking { BookingId = 1, SessionId = 1, Status = "Confirmed" } });

        var service = CreateService();
        await service.PromoteNextInLineAsync(1);

        _waitlistRepository.Verify(r => r.RemoveAsync(It.IsAny<int>()), Times.Never);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
        _notificationService.Verify(n => n.SendWaitlistNotificationAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task PromoteNextInLineAsync_NoOneWaiting_DoesNothing()
    {
        _waitlistRepository.Setup(r => r.GetNextInLineAsync(1)).ReturnsAsync((Waitlist?)null);

        var service = CreateService();
        await service.PromoteNextInLineAsync(1);

        _sessionRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }
}
