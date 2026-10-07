using Microsoft.EntityFrameworkCore;
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
            context ?? NewInMemoryContext());
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

    private static BookingRequest ValidRequest(bool consentSigned = true) => new()
    {
        Email = "new.client@example.com",
        FullName = "New Client",
        PhoneNumber = "0821234567",
        PaymentType = "per-class",
        Method = "EFT",
        SessionId = 1,
        ConsentSigned = consentSigned
    };

    [Fact]
    public async Task CreateBookingAsync_NewClientWithoutConsent_IsRejectedAndNeverCreatesTheClient()
    {
        _clientRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Client?)null);

        var service = CreateService();
        var result = await service.CreateBookingAsync(ValidRequest(consentSigned: false));

        Assert.False(result.Success);
        Assert.Contains("consent", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _clientRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_NewClientWithConsent_CreatesClientFlaggedAsNew()
    {
        _clientRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Client?)null);
        Client? created = null;
        _clientRepository
            .Setup(r => r.AddAsync(It.IsAny<Client>()))
            .Callback<Client>(c => { c.ClientId = 42; created = c; })
            .Returns(Task.CompletedTask);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => b.BookingId = 7)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.CreateBookingAsync(ValidRequest(consentSigned: true));

        Assert.True(result.Success);
        Assert.NotNull(created);
        Assert.True(created!.IsNew);
        Assert.Equal("new.client@example.com", created.Email);
        _clientRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Once);
    }

    [Fact]
    public async Task CheckClientStatusAsync_ExistingClient_ReturnsPrefilledDetailsAndIsNotNew()
    {
        var existing = new Client
        {
            ClientId = 5,
            Email = "returning@example.com",
            FullName = "Returning Client",
            PhoneNumber = "0839876543",
            IsNew = false
        };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);

        var service = CreateService();
        var status = await service.CheckClientStatusAsync("returning@example.com");

        Assert.False(status.IsNew);
        Assert.Equal("Returning Client", status.FullName);
        Assert.Equal("0839876543", status.PhoneNumber);
    }

    [Fact]
    public async Task CreateBookingAsync_ReturningClient_DoesNotRequireConsentAndSkipsIntakeForm()
    {
        // Full name and phone match what ValidRequest() submits exactly, so
        // this test still isolates what it's named for: the already-not-new
        // flag causing no redundant update. (Editing those details onto an
        // existing client is covered on its own in
        // CreateBookingAsync_ExistingClient_*.)
        var existing = new Client { ClientId = 5, Email = "returning@example.com", FullName = "New Client", PhoneNumber = "0821234567", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>()))
            .Callback<Booking>(b => b.BookingId = 9)
            .Returns(Task.CompletedTask);

        using var context = NewInMemoryContext();
        var service = CreateService(context);

        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";
        var result = await service.CreateBookingAsync(request);

        Assert.True(result.Success);
        // Only a new client's booking writes an IntakeForm row.
        Assert.Equal(0, await context.IntakeForms.CountAsync());
        // Already not-new and nothing edited, so no update at all.
        _clientRepository.Verify(r => r.UpdateAsync(It.IsAny<Client>()), Times.Never);
    }

    // ---- Saving an existing client's edited details (the readonly-field
    // follow-up bug: the form let a returning client edit these, but
    // nothing saved the edit) ----

    [Fact]
    public async Task CreateBookingAsync_ExistingClient_PhoneNumberChanged_SavesTrimmedPhoneNumber()
    {
        var existing = new Client { ClientId = 5, Email = "returning@example.com", FullName = "New Client", PhoneNumber = "0000000000", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";
        request.PhoneNumber = "  0821234567  "; // a form field can submit surrounding whitespace

        var result = await service.CreateBookingAsync(request);

        Assert.True(result.Success);
        Assert.Equal("0821234567", existing.PhoneNumber);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.PhoneNumber == "0821234567")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_ExistingClient_FullNameChanged_SavesTrimmedFullName()
    {
        var existing = new Client { ClientId = 5, Email = "returning@example.com", FullName = "Old Name", PhoneNumber = "0821234567", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";
        request.FullName = "  New Client  ";

        var result = await service.CreateBookingAsync(request);

        Assert.True(result.Success);
        Assert.Equal("New Client", existing.FullName);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.FullName == "New Client")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_ExistingClient_OnlyWhitespaceDiffers_DoesNotUpdate()
    {
        var existing = new Client { ClientId = 5, Email = "returning@example.com", FullName = "New Client", PhoneNumber = "0821234567", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";
        request.FullName = "  New Client  ";
        request.PhoneNumber = "  0821234567  ";

        var result = await service.CreateBookingAsync(request);

        Assert.True(result.Success);
        _clientRepository.Verify(r => r.UpdateAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_ExistingClient_DetailsChanged_NeverChangesEmailOrPaymentType()
    {
        var existing = new Client { ClientId = 5, Email = "returning@example.com", FullName = "Old Name", PhoneNumber = "0000000000", PaymentType = "monthly", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        // ValidRequest's PaymentType is "per-class" - deliberately different
        // from the client's stored "monthly", since PaymentType is a
        // per-booking choice, not a client detail to sync.
        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";

        var result = await service.CreateBookingAsync(request);

        Assert.True(result.Success);
        Assert.Equal("returning@example.com", existing.Email);
        Assert.Equal("monthly", existing.PaymentType);
        _clientRepository.Verify(r => r.UpdateAsync(It.Is<Client>(c => c.Email == "returning@example.com" && c.PaymentType == "monthly")), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_NewClient_DetailsSavingLogicDoesNotApply()
    {
        _clientRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((Client?)null);
        Client? created = null;
        _clientRepository
            .Setup(r => r.AddAsync(It.IsAny<Client>()))
            .Callback<Client>(c => { c.ClientId = 50; created = c; })
            .Returns(Task.CompletedTask);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession());
        _bookingRepository.Setup(r => r.GetBookingsBySessionAsync(1)).ReturnsAsync(Enumerable.Empty<Booking>());
        _bookingRepository.Setup(r => r.CreateBookingAsync(It.IsAny<Booking>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.CreateBookingAsync(ValidRequest(consentSigned: true));

        Assert.True(result.Success);
        Assert.NotNull(created);
        _clientRepository.Verify(r => r.AddAsync(It.IsAny<Client>()), Times.Once);
        // The new-client path builds the Client straight from the request
        // and never reaches the existing-client edit check at all.
        _clientRepository.Verify(r => r.UpdateAsync(It.IsAny<Client>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_SessionAtCapacity_ReturnsRequiresWaitlistInsteadOfBooking()
    {
        var existing = new Client { ClientId = 5, Email = "returning@example.com", IsNew = false };
        _clientRepository.Setup(r => r.GetByEmailAsync("returning@example.com")).ReturnsAsync(existing);
        _sessionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(OpenSession(capacity: 1));
        _bookingRepository
            .Setup(r => r.GetBookingsBySessionAsync(1))
            .ReturnsAsync(new[] { new Booking { BookingId = 1, SessionId = 1, Status = "Confirmed" } });

        var service = CreateService();
        var request = ValidRequest(consentSigned: false);
        request.Email = "returning@example.com";
        var result = await service.CreateBookingAsync(request);

        Assert.False(result.Success);
        Assert.True(result.RequiresWaitlist);
        Assert.Equal(5, result.ClientId);
        _bookingRepository.Verify(r => r.CreateBookingAsync(It.IsAny<Booking>()), Times.Never);
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
