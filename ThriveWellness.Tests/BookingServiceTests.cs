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
}
