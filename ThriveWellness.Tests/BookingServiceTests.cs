using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class BookingServiceTests
{
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<IWaitlistService> _waitlistService = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILocationRepository> _locationRepository = new();
    private readonly Mock<IPaymentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IPaymentGateway> _gateway = new();

    // BookingService writes IntakeForm rows straight through
    // ApplicationDbContext rather than a repository interface (see its own
    // comment on that field). An EF Core in-memory database - a different
    // database per test so they can't see each other's data - stands in for
    // it so this still needs no real database connection.
    private static ApplicationDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private BookingService CreateService(ApplicationDbContext? context = null)
    {
        return new BookingService(
            _clientRepository.Object,
            _bookingRepository.Object,
            _sessionRepository.Object,
            _waitlistService.Object,
            _paymentRepository.Object,
            _notificationService.Object,
            // The real RefundService over the mocked repositories and
            // gateway, so the cancellation tests see the refund it records.
            new RefundService(_paymentRepository.Object, _transactionRepository.Object, _gateway.Object, NullLogger<RefundService>.Instance),
            _locationRepository.Object,
            TimeProvider.System,
            context ?? NewInMemoryContext(),
            NullLogger<BookingService>.Instance);
    }

    private static Session OpenSession(int capacity = 10) => new()
    {
        SessionId = 1,
        LocationId = 1,
        SessionType = "group",
        Date = DateTime.Today.AddDays(1),
        Time = new TimeSpan(9, 0, 0),
        Capacity = capacity,
        IsOpen = true
    };

    private const int SignedInClientId = 5;

    private static BookingRequest ValidRequest(bool consentSigned = true) => new()
    {
        ClientId = SignedInClientId,
        FullName = "Jane Doe",
        PhoneNumber = "0821234567",
        PaymentType = "per-class",
        Method = "EFT",
        SessionId = 1,
        ConsentSigned = consentSigned
    };

    // The signed-in client as stored. previousBookings = 0 makes this their
    // first booking (intake form and consent); anything more makes them a
    // returning client.
    private Client SignedInClient(int previousBookings, string fullName = "Jane Doe", string phone = "0821234567", bool isNew = false, string paymentType = "per-class")
    {
        var client = new Client
        {
            ClientId = SignedInClientId,
            Email = "jane@example.com",
            FullName = fullName,
            PhoneNumber = phone,
            IsNew = isNew,
            PaymentType = paymentType
        };
        _clientRepository.Setup(r => r.GetByIdAsync(SignedInClientId)).ReturnsAsync(client);
        _bookingRepository
            .Setup(r => r.GetBookingsByClientAsync(SignedInClientId))
            .ReturnsAsync(Enumerable.Range(1, previousBookings).Select(i => new Booking { BookingId = 100 + i, ClientId = SignedInClientId }).ToList());
        return client;
    }

    private void OpenSessionWithRoom()
    {
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => b.BookingId = 7)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateBookingAsync_AttachesTheBookingToTheSignedInClient()
    {
        SignedInClient(previousBookings: 2);
        OpenSessionWithRoom();

        var result = await CreateService().CreateBookingAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Equal(SignedInClientId, result.ClientId);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.Is<Booking>(b => b.ClientId == SignedInClientId && b.SessionId == 1 && b.Status == "Awaiting Payment")), Times.Once);
        _paymentRepository.Verify(r => r.CreateAsync(It.Is<Payment>(p => p.BookingId == 7 && p.Status == "Pending" && p.Method == "EFT")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_NeverCreatesAClient()
    {
        // Clients are only ever created at sign-in now (ClientAccountService).
        SignedInClient(previousBookings: 0);
        OpenSessionWithRoom();

        await CreateService().CreateBookingAsync(ValidRequest());

        _clientRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_UnknownClient_FailsWithoutBooking()
    {
        _clientRepository.Setup(r => r.GetByIdAsync(SignedInClientId)).ReturnsAsync((Client?)null);

        var result = await CreateService().CreateBookingAsync(ValidRequest());

        Assert.False(result.Success);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_FirstBookingWithoutConsent_IsRejectedAndBooksNothing()
    {
        SignedInClient(previousBookings: 0, isNew: true);
        OpenSessionWithRoom();

        var result = await CreateService().CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.False(result.Success);
        Assert.Contains("consent", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_FirstBookingWithConsent_WritesTheIntakeFormAndKeepsTheClientNew()
    {
        var client = SignedInClient(previousBookings: 0, isNew: true);
        OpenSessionWithRoom();
        using var context = NewInMemoryContext();

        var request = ValidRequest(consentSigned: true);
        request.MedicalNotes = "Old knee injury";
        var result = await CreateService(context).CreateBookingAsync(request);

        Assert.True(result.Success);
        var intake = await context.IntakeForms.SingleAsync();
        Assert.Equal(7, intake.BookingId);
        Assert.Equal("Old knee injury", intake.MedicalNotes);
        Assert.True(intake.ConsentSigned);
        // Still new until their next booking, so the first-timer location
        // email goes out on the day of this class.
        Assert.True(client.IsNew);
    }

    [Fact]
    public async Task CreateBookingAsync_ReturningClient_DoesNotRequireConsentAndSkipsIntakeForm()
    {
        SignedInClient(previousBookings: 1);
        OpenSessionWithRoom();
        using var context = NewInMemoryContext();

        var result = await CreateService(context).CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.True(result.Success);
        Assert.Equal(0, await context.IntakeForms.CountAsync());
        // Already not new and nothing edited, so no update at all.
        _clientRepository.Verify(r => r.UpdateAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_ReturningClientStillMarkedNew_IsNoLongerNew()
    {
        var client = SignedInClient(previousBookings: 1, isNew: true);
        OpenSessionWithRoom();

        await CreateService().CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.False(client.IsNew);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => !c.IsNew)), Times.Once);
    }

    // ---- Saving the client's edited details (the returning client's edited
    // phone number used to be lost) ----

    [Fact]
    public async Task CreateBookingAsync_ReturningClient_PhoneNumberChanged_SavesTrimmedPhoneNumber()
    {
        var client = SignedInClient(previousBookings: 3, phone: "0000000000");
        OpenSessionWithRoom();

        var request = ValidRequest(consentSigned: false);
        request.PhoneNumber = "  0821234567  "; // a form field can submit surrounding whitespace
        var result = await CreateService().CreateBookingAsync(request);

        Assert.True(result.Success);
        Assert.Equal("0821234567", client.PhoneNumber);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.ClientId == SignedInClientId && c.PhoneNumber == "0821234567")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_FirstBooking_SavesThePhoneNumberToAGoogleCreatedClient()
    {
        // A client created at Google sign-in has no phone number yet.
        var client = SignedInClient(previousBookings: 0, phone: string.Empty, isNew: true, paymentType: string.Empty);
        OpenSessionWithRoom();

        await CreateService().CreateBookingAsync(ValidRequest(consentSigned: true));

        Assert.Equal("0821234567", client.PhoneNumber);
        Assert.Equal("per-class", client.PaymentType);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.PhoneNumber == "0821234567")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_FullNameChanged_SavesTrimmedFullName()
    {
        var client = SignedInClient(previousBookings: 1, fullName: "Old Name");
        OpenSessionWithRoom();

        var request = ValidRequest(consentSigned: false);
        request.FullName = "  Jane Doe  ";
        await CreateService().CreateBookingAsync(request);

        Assert.Equal("Jane Doe", client.FullName);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.FullName == "Jane Doe")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_OnlyWhitespaceDiffers_DoesNotUpdate()
    {
        SignedInClient(previousBookings: 1);
        OpenSessionWithRoom();

        var request = ValidRequest(consentSigned: false);
        request.FullName = "  Jane Doe  ";
        request.PhoneNumber = "  0821234567  ";
        await CreateService().CreateBookingAsync(request);

        _clientRepository.Verify(r => r.UpdateAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_DetailsChanged_NeverChangesEmailOrAReturningClientsPaymentType()
    {
        var client = SignedInClient(previousBookings: 1, fullName: "Old Name", phone: "0000000000", paymentType: "monthly");
        OpenSessionWithRoom();

        // ValidRequest's PaymentType is "per-class" - deliberately different
        // from the client's stored "monthly", since after the first booking
        // PaymentType is a per-booking choice, not a client detail to sync.
        await CreateService().CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.Equal("jane@example.com", client.Email);
        Assert.Equal("monthly", client.PaymentType);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.Email == "jane@example.com" && c.PaymentType == "monthly")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_SessionAtCapacity_ReturnsRequiresWaitlistInsteadOfBooking()
    {
        SignedInClient(previousBookings: 1);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 1));
        _bookingRepository
            .Setup(r => r.GetBookingsBySessionAsync(1))
            .ReturnsAsync(new[] { new Booking { BookingId = 1, SessionId = 1, Status = "Confirmed" } });

        var result = await CreateService().CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.False(result.Success);
        Assert.True(result.RequiresWaitlist);
        Assert.Equal(SignedInClientId, result.ClientId);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task IsFirstBookingAsync_DependsOnWhetherTheClientHasBookedBefore(int previousBookings, bool expected)
    {
        SignedInClient(previousBookings);

        Assert.Equal(expected, await CreateService().IsFirstBookingAsync(SignedInClientId));
    }

    [Fact]
    public async Task CancelBookingAsync_ValidToken_UpdatesStatusToCancelledAndPromotesWaitlist()
    {
        var booking = new Booking { BookingId = 3, SessionId = 1, Status = "Confirmed", CancellationToken = "valid-token" };
        _bookingRepository.Setup(r => r.GetByCancellationTokenAsync("valid-token")).ReturnsAsync(booking);

        var service = CreateService();
        var result = await service.CancelBookingAsync("valid-token");

        Assert.True(result.Success);
        Assert.Equal("Cancelled", booking.Status);
        _bookingRepository.Verify(r => r.UpdateAsync(It.Is<Booking>(b => b.Status == "Cancelled")), Times.Once);
        _waitlistService.Verify(w => w.PromoteNextInLineAsync(1), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_InvalidToken_IsRejected()
    {
        _bookingRepository.Setup(r => r.GetByCancellationTokenAsync("bad-token")).ReturnsAsync((Booking?)null);

        var service = CreateService();
        var result = await service.CancelBookingAsync("bad-token");

        Assert.False(result.Success);
        Assert.Equal("Invalid cancellation link.", result.ErrorMessage);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelledToken_IsRejected()
    {
        var booking = new Booking { BookingId = 3, SessionId = 1, Status = "Cancelled", CancellationToken = "used-token" };
        _bookingRepository.Setup(r => r.GetByCancellationTokenAsync("used-token")).ReturnsAsync(booking);

        var service = CreateService();
        var result = await service.CancelBookingAsync("used-token");

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
        _waitlistService.Verify(w => w.PromoteNextInLineAsync(It.IsAny<int>()), Times.Never);
    }

    // Client cancellation from the dashboard.

    private Booking ClientBooking(string status = "Confirmed", DateTime? sessionStart = null)
    {
        var start = sessionStart ?? DateTime.Today.AddDays(1).AddHours(9);
        var booking = new Booking { BookingId = 30, ClientId = SignedInClientId, SessionId = 4, Status = status, CancellationToken = "tok" };
        _bookingRepository.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(booking);
        _sessionRepository.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(new Session
        {
            SessionId = 4,
            LocationId = 1,
            SessionType = "group",
            Date = start.Date,
            Time = start.TimeOfDay,
            Capacity = 10,
            IsOpen = true
        });
        return booking;
    }

    private Payment PaidWith(string method, decimal amount = 120m)
    {
        var payment = new Payment { PaymentId = 60, BookingId = 30, Method = method, Amount = amount, Status = PaymentStatuses.Confirmed, PaymentType = "per-class" };
        _paymentRepository.Setup(r => r.GetByBookingIdAsync(30)).ReturnsAsync(payment);
        _paymentRepository
            .Setup(r => r.UpdateStatusAsync(60, It.IsAny<string>()))
            .Callback<int, string>((_, status) => payment.Status = status)
            .Returns(Task.CompletedTask);
        return payment;
    }

    [Fact]
    public async Task CancelByClientAsync_OwnUpcomingBooking_CancelsAndPromotesTheWaitlist()
    {
        var booking = ClientBooking();

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
        Assert.Equal("Cancelled", booking.Status);
        _waitlistService.Verify(w => w.PromoteNextInLineAsync(4), Times.Once);
        Assert.Equal(RefundOutcome.None, result.Refund.Outcome);
    }

    [Fact]
    public async Task CancelByClientAsync_ClientCancelling_SendsNoCancellationEmail()
    {
        ClientBooking();

        await CreateService().CancelByClientAsync(30, SignedInClientId);

        _notificationService.Verify(n => n.SendCancellationEmailAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CancelByClientAsync_SomeoneElsesBooking_IsRefusedAndNothingChanges()
    {
        var booking = ClientBooking();

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId + 1);

        Assert.False(result.Success);
        Assert.True(result.NotOwner);
        Assert.Equal("Confirmed", booking.Status);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
        _waitlistService.Verify(w => w.PromoteNextInLineAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CancelByClientAsync_ClassAlreadyStarted_IsRefused()
    {
        var booking = ClientBooking(sessionStart: DateTime.Now.AddMinutes(-5));

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.False(result.Success);
        Assert.Contains("started", result.ErrorMessage);
        Assert.Equal("Confirmed", booking.Status);
        _bookingRepository.Verify(r => r.UpdateAsync(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task CancelByClientAsync_ClassLaterToday_CanStillBeCancelled()
    {
        ClientBooking(sessionStart: DateTime.Now.AddMinutes(30));

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CancelByClientAsync_Twice_SecondTimeIsRefused()
    {
        ClientBooking();
        var service = CreateService();

        var first = await service.CancelByClientAsync(30, SignedInClientId);
        var second = await service.CancelByClientAsync(30, SignedInClientId);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains("already", second.ErrorMessage);
        _waitlistService.Verify(w => w.PromoteNextInLineAsync(4), Times.Once);
    }

    [Fact]
    public async Task CancelByClientAsync_PaidByCard_RefundsTheChargeAndMarksThePaymentRefunded()
    {
        ClientBooking();
        var payment = PaidWith(PaymentMethods.Card, 450m);
        var charge = new PaymentTransaction { PaymentTransactionId = 1, PaymentId = 60, Amount = 450m, Status = PaymentTransactionStatus.Succeeded, GatewayReference = "TW-PAID000001" };
        _transactionRepository.Setup(r => r.GetChargeForPaymentAsync(60)).ReturnsAsync(charge);
        _gateway
            .Setup(g => g.RefundAsync("TW-PAID000001", 450m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult { Success = true, RefundReference = "TW-RFTEST0001" });

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
        Assert.Equal(RefundOutcome.CardRefunded, result.Refund.Outcome);
        Assert.Equal(450m, result.Refund.Amount);
        Assert.Equal(PaymentStatuses.Refunded, payment.Status);
        Assert.Equal(PaymentTransactionStatus.Refunded, charge.Status);
        _transactionRepository.Verify(r => r.UpdateAsync(charge), Times.Once);
    }

    [Fact]
    public async Task CancelByClientAsync_CardRefundRejected_FallsBackToRefundDue()
    {
        ClientBooking();
        var payment = PaidWith(PaymentMethods.Card);
        _transactionRepository.Setup(r => r.GetChargeForPaymentAsync(60)).ReturnsAsync(
            new PaymentTransaction { PaymentId = 60, Amount = 120m, Status = PaymentTransactionStatus.Succeeded, GatewayReference = "TW-PAID000002" });
        _gateway
            .Setup(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult { Success = false });

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
        Assert.Equal(RefundOutcome.RefundDue, result.Refund.Outcome);
        Assert.Equal(PaymentStatuses.RefundDue, payment.Status);
    }

    [Theory]
    [InlineData(PaymentMethods.Eft)]
    [InlineData(PaymentMethods.Cash)]
    public async Task CancelByClientAsync_PaidByEftOrCash_MarksRefundDue(string method)
    {
        ClientBooking();
        var payment = PaidWith(method);

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
        Assert.Equal(RefundOutcome.RefundDue, result.Refund.Outcome);
        Assert.Equal(PaymentStatuses.RefundDue, payment.Status);
        _gateway.Verify(g => g.RefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelByClientAsync_NothingPaid_LeavesThePaymentAlone()
    {
        ClientBooking(status: "Awaiting Payment");
        var payment = new Payment { PaymentId = 60, BookingId = 30, Method = PaymentMethods.Card, Amount = 120m, Status = PaymentStatuses.Pending };
        _paymentRepository.Setup(r => r.GetByBookingIdAsync(30)).ReturnsAsync(payment);

        var result = await CreateService().CancelByClientAsync(30, SignedInClientId);

        Assert.True(result.Success);
        Assert.Equal(RefundOutcome.None, result.Refund.Outcome);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        _paymentRepository.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetClientCancellationAsync_PreviewsTheRefundWithoutChangingAnything()
    {
        var booking = ClientBooking();
        var payment = PaidWith(PaymentMethods.Card);

        var preview = await CreateService().GetClientCancellationAsync(30, SignedInClientId);

        Assert.Equal(CancelPreviewStatus.Ready, preview.Status);
        Assert.Equal(RefundOutcome.CardRefunded, preview.Refund.Outcome);
        Assert.Equal("Confirmed", booking.Status);
        Assert.Equal(PaymentStatuses.Confirmed, payment.Status);
    }

    [Fact]
    public async Task GetClientCancellationAsync_SomeoneElsesBooking_IsNotOwner()
    {
        ClientBooking();

        var preview = await CreateService().GetClientCancellationAsync(30, SignedInClientId + 1);

        Assert.Equal(CancelPreviewStatus.NotOwner, preview.Status);
    }

    [Fact]
    public async Task CancelByAdminAsync_PaidByEft_AlsoMarksRefundDue()
    {
        ClientBooking();
        var payment = PaidWith(PaymentMethods.Eft);

        var result = await CreateService().CancelByAdminAsync(30);

        Assert.True(result.Success);
        Assert.Equal(PaymentStatuses.RefundDue, payment.Status);
    }
}
