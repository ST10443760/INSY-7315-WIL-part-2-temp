using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class WaitlistServiceTests
{
    private readonly Mock<IWaitlistRepository> _waitlistRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();

    // WaitlistService wraps a promotion's writes in one explicit
    // transaction (see PromoteEntryAsync) - the in-memory provider doesn't
    // support real transactions, so the warning it would otherwise raise is
    // suppressed here. The transaction itself becomes a no-op against this
    // provider, which is fine: nothing in these tests depends on real
    // commit/rollback semantics, only on which repository calls happen.
    private static ApplicationDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private WaitlistService CreateService(ApplicationDbContext? context = null) => new(
        _waitlistRepository.Object,
        _bookingRepository.Object,
        _sessionRepository.Object,
        _clientRepository.Object,
        _paymentRepository.Object,
        _notificationService.Object,
        context ?? NewInMemoryContext(),
        NullLogger<WaitlistService>.Instance);

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

    // Never the studio's real plan data - just enough to pick a branch.
    private static Client FakeClient(int id = 7, string paymentType = "") => new()
    {
        ClientId = id,
        FullName = "Jane Doe",
        Email = "jane@example.com",
        PaymentType = paymentType
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
    public async Task PromoteNextInLineAsync_RoomAvailable_AlsoCreatesAPendingPayment()
    {
        // Part 3's fix: promoting someone used to create a Booking only,
        // with no Payment row at all - they'd never show up on Pending
        // Payments. This confirms the shared routine now creates one here
        // too, the same way the admin's "Add to class" button does.
        var next = new Waitlist { WaitlistId = 1, SessionId = 1, ClientId = 7, Position = 1 };
        _waitlistRepository.Setup(r => r.GetNextInLineAsync(1)).ReturnsAsync(next);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 2));
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Waitlist>());
        _clientRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(FakeClient(id: 7, paymentType: "monthly"));
        Booking? created = null;
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => { b.BookingId = 55; created = b; })
            .Returns(Task.CompletedTask);
        Payment? createdPayment = null;
        _paymentRepository
            .Setup(r => r.CreateAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => createdPayment = p)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.PromoteNextInLineAsync(1);

        Assert.NotNull(createdPayment);
        Assert.Equal(created!.BookingId, createdPayment!.BookingId);
        Assert.Equal("Pending", createdPayment.Status);
        Assert.Equal("monthly", createdPayment.PaymentType);
        Assert.Equal("EFT", createdPayment.Method);
        Assert.Equal(PaymentPricing.MonthlyPrice, createdPayment.Amount);
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

    // ---- AddToClassAsync (the admin's manual "Add to class" button) ----

    private void SetUpPromotableEntry(Waitlist entry, Client client, Session session, IEnumerable<Booking>? existingBookings = null)
    {
        _waitlistRepository.Setup(r => r.GetByIdAsync(entry.WaitlistId)).ReturnsAsync(entry);
        _clientRepository.Setup(r => r.GetByIdAsync(entry.ClientId)).ReturnsAsync(client);
        _sessionRepository.Setup(r => r.GetByIdAsync(entry.SessionId)).ReturnsAsync(session);
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(entry.SessionId)).ReturnsAsync(existingBookings ?? Enumerable.Empty<Booking>());
        _waitlistRepository.Setup(r => r.GetBySessionAsync(entry.SessionId)).ReturnsAsync(Enumerable.Empty<Waitlist>());
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => b.BookingId = 99)
            .Returns(Task.CompletedTask);
    }

    [Theory]
    [InlineData("per-class")]
    [InlineData("monthly")]
    public async Task AddToClassAsync_ClientHasAValidPlan_CreatesAwaitingPaymentBookingAndMatchingPendingPayment(string paymentType)
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var client = FakeClient(paymentType: paymentType);
        SetUpPromotableEntry(entry, client, OpenSession(capacity: 10));
        Booking? createdBooking = null;
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => { b.BookingId = 99; createdBooking = b; })
            .Returns(Task.CompletedTask);
        Payment? createdPayment = null;
        _paymentRepository
            .Setup(r => r.CreateAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => createdPayment = p)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(createdBooking);
        Assert.Equal("Awaiting Payment", createdBooking!.Status);
        Assert.False(string.IsNullOrEmpty(createdBooking.CancellationToken));
        Assert.NotNull(createdPayment);
        Assert.Equal(createdBooking.BookingId, createdPayment!.BookingId);
        Assert.Equal("Pending", createdPayment.Status);
        Assert.Equal("EFT", createdPayment.Method);
        Assert.Equal(paymentType, createdPayment.PaymentType);
        Assert.Equal(PaymentPricing.GetAmountForPaymentType(paymentType), createdPayment.Amount);
    }

    [Fact]
    public async Task AddToClassAsync_ClientHasNoStoredPlan_DefaultsToPerClassAndEft()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var client = FakeClient(paymentType: string.Empty);
        SetUpPromotableEntry(entry, client, OpenSession(capacity: 10));
        Payment? createdPayment = null;
        _paymentRepository
            .Setup(r => r.CreateAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => createdPayment = p)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(createdPayment);
        Assert.Equal("per-class", createdPayment!.PaymentType);
        Assert.Equal("EFT", createdPayment.Method);
        Assert.Equal(PaymentPricing.PerClassPrice, createdPayment.Amount);
    }

    [Fact]
    public async Task AddToClassAsync_Success_RemovesEntryAndRenumbersLaterPositions()
    {
        var entry = new Waitlist { WaitlistId = 2, ClientId = 7, SessionId = 1, Position = 2 };
        var laterOne = new Waitlist { WaitlistId = 3, ClientId = 8, SessionId = 1, Position = 3 };
        var laterTwo = new Waitlist { WaitlistId = 4, ClientId = 9, SessionId = 1, Position = 4 };
        _waitlistRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(entry);
        _clientRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(FakeClient());
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 10));
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        // Queried again after the removal, so position 2 (the one removed) is gone.
        _waitlistRepository.Setup(r => r.GetBySessionAsync(1)).ReturnsAsync(new[] { laterOne, laterTwo });
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.AddToClassAsync(2);

        Assert.True(result.Success);
        _waitlistRepository.Verify(r => r.RemoveAsync(2), Times.Once);
        Assert.Equal(2, laterOne.Position);
        Assert.Equal(3, laterTwo.Position);
        _waitlistRepository.Verify(r => r.UpdateAsync(laterOne), Times.Once);
        _waitlistRepository.Verify(r => r.UpdateAsync(laterTwo), Times.Once);
    }

    [Fact]
    public async Task AddToClassAsync_Success_SendsThePaymentDetailsEmailExactlyOnce()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        SetUpPromotableEntry(entry, FakeClient(), OpenSession(capacity: 10));

        var service = CreateService();
        await service.AddToClassAsync(1);

        _notificationService.Verify(n => n.SendWaitlistNotificationAsync(It.IsAny<Booking>()), Times.Once);
    }

    [Fact]
    public async Task AddToClassAsync_EmailSendFails_StillKeepsTheBookingAndPayment()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        SetUpPromotableEntry(entry, FakeClient(), OpenSession(capacity: 10));
        _notificationService
            .Setup(n => n.SendWaitlistNotificationAsync(It.IsAny<Booking>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid returned 401 sending to jane@example.com"));

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.True(result.Success);
        Assert.False(result.EmailSent);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Once);
        _paymentRepository.Verify(r => r.CreateAsync(It.IsAny<Payment>()), Times.Once);
        _waitlistRepository.Verify(r => r.RemoveAsync(1), Times.Once);
    }

    [Fact]
    public async Task AddToClassAsync_EntryMissing_ReturnsErrorAndMakesNoChanges()
    {
        _waitlistRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Waitlist?)null);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_ClientMissing_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        _waitlistRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(entry);
        _clientRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync((Client?)null);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_SessionMissing_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        _waitlistRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(entry);
        _clientRepository.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(FakeClient());
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Session?)null);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_SessionFull_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var existingBooking = new Booking { BookingId = 1, ClientId = 50, SessionId = 1, Status = "Confirmed" };
        SetUpPromotableEntry(entry, FakeClient(), OpenSession(capacity: 1), new[] { existingBooking });

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.Contains("full", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _waitlistRepository.Verify(r => r.RemoveAsync(It.IsAny<int>()), Times.Never);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
        _paymentRepository.Verify(r => r.CreateAsync(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_SessionClosed_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var closedSession = OpenSession(capacity: 10);
        closedSession.IsOpen = false;
        SetUpPromotableEntry(entry, FakeClient(), closedSession);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.Contains("closed", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
        _paymentRepository.Verify(r => r.CreateAsync(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_SessionInThePast_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var pastSession = OpenSession(capacity: 10);
        pastSession.Date = DateTime.Today.AddDays(-1);
        SetUpPromotableEntry(entry, FakeClient(), pastSession);

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.Contains("past", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
        _paymentRepository.Verify(r => r.CreateAsync(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_ClientAlreadyHasAnActiveBookingForTheSession_ReturnsErrorAndMakesNoChanges()
    {
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var ownBooking = new Booking { BookingId = 5, ClientId = 7, SessionId = 1, Status = "Confirmed" };
        SetUpPromotableEntry(entry, FakeClient(), OpenSession(capacity: 10), new[] { ownBooking });

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _waitlistRepository.Verify(r => r.RemoveAsync(It.IsAny<int>()), Times.Never);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
        _paymentRepository.Verify(r => r.CreateAsync(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task AddToClassAsync_ClientHasOnlyACancelledBooking_IsStillAllowed()
    {
        // A cancelled booking isn't "active" - the capacity guard and the
        // already-booked guard both ignore it, matching every other
        // capacity check in the app.
        var entry = new Waitlist { WaitlistId = 1, ClientId = 7, SessionId = 1, Position = 1 };
        var cancelledBooking = new Booking { BookingId = 5, ClientId = 7, SessionId = 1, Status = "Cancelled" };
        SetUpPromotableEntry(entry, FakeClient(), OpenSession(capacity: 10), new[] { cancelledBooking });

        var service = CreateService();
        var result = await service.AddToClassAsync(1);

        Assert.True(result.Success);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Once);
    }
}
