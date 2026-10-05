using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class NotificationServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<IClientRepository> _clientRepository = new();
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<ILocationRepository> _locationRepository = new();
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IEmailSender> _emailSender = new();

    // NotificationService writes Notification rows straight through
    // ApplicationDbContext (see WriteNotificationRecordAsync) rather than a
    // repository interface - an EF Core in-memory database stands in for it,
    // same pattern BookingServiceTests already uses for IntakeForms.
    private static ApplicationDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IConfiguration FakeConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"] = "https://test.local"
            })
            .Build();

    // Obviously-fake banking details - never the studio's real account.
    private static PaymentOptions FakePaymentOptions() => new()
    {
        AccountHolder = "Test Studio Holdings",
        Bank = "Test Bank",
        AccountNumber = "0000000000"
    };

    private NotificationService CreateService(PaymentOptions? paymentOptions = null, ApplicationDbContext? context = null)
    {
        return new NotificationService(
            _bookingRepository.Object,
            _clientRepository.Object,
            _sessionRepository.Object,
            _locationRepository.Object,
            _paymentRepository.Object,
            _emailSender.Object,
            context ?? NewInMemoryContext(),
            FakeConfiguration(),
            Options.Create(paymentOptions ?? FakePaymentOptions()),
            NullLogger<NotificationService>.Instance);
    }

    private static Client FakeClient() => new()
    {
        ClientId = 1,
        FullName = "Jane Doe",
        Email = "jane@example.com"
    };

    private static Booking FakeBooking(int bookingId = 42) => new()
    {
        BookingId = bookingId,
        ClientId = 1,
        SessionId = 1,
        Status = "Awaiting Payment",
        CancellationToken = "test-token"
    };

    private static Session FakeSession() => new()
    {
        SessionId = 1,
        LocationId = 1,
        SessionType = "group",
        Date = DateTime.Today.AddDays(3),
        Time = new TimeSpan(9, 0, 0),
        Capacity = 10,
        IsOpen = true
    };

    private static Location FakeLocation() => new()
    {
        LocationId = 1,
        Name = "Test Studio",
        Address = "Test Suburb"
    };

    private static Payment FakePayment(string paymentType, decimal amount, int bookingId = 42) => new()
    {
        PaymentId = 1,
        BookingId = bookingId,
        Method = "EFT",
        Amount = amount,
        Status = "Pending",
        PaymentType = paymentType
    };

    private void SetUpLookupsFor(Booking booking, Payment payment)
    {
        _clientRepository.Setup(r => r.GetByIdAsync(booking.ClientId)).ReturnsAsync(FakeClient());
        _sessionRepository.Setup(r => r.GetByIdAsync(booking.SessionId)).ReturnsAsync(FakeSession());
        _locationRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(FakeLocation());
        _paymentRepository.Setup(r => r.GetByBookingIdAsync(booking.BookingId)).ReturnsAsync(payment);
    }

    [Theory]
    [InlineData("per-class")]
    [InlineData("monthly")]
    public async Task SendConfirmationEmailAsync_WithConfiguredPaymentDetails_IncludesThemAndTheRightAmount(string paymentType)
    {
        // The expected amount comes from the same shared PaymentPricing
        // helper the email-building code uses, not a hardcoded number here -
        // the whole point of sharing it is that there's only one place R120
        // and R450 are written down.
        var expectedAmount = PaymentPricing.GetAmountForPaymentType(paymentType);
        var booking = FakeBooking();
        var payment = FakePayment(paymentType, expectedAmount);
        SetUpLookupsFor(booking, payment);

        string? sentHtml = null;
        _emailSender
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => sentHtml = html)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.SendConfirmationEmailAsync(booking);

        Assert.NotNull(sentHtml);
        Assert.Contains("Test Studio Holdings", sentHtml);
        Assert.Contains("Test Bank", sentHtml);
        Assert.Contains("0000000000", sentHtml);
        Assert.Contains("Jane Doe", sentHtml);
        Assert.Contains(booking.BookingId.ToString(), sentHtml);
        Assert.Contains($"R{expectedAmount}", sentHtml);
        Assert.Contains("Cash is also accepted", sentHtml);
    }

    [Fact]
    public async Task SendConfirmationEmailAsync_MissingPaymentConfig_FallsBackSafelyWithoutBlanksOrPlaceholderText()
    {
        var booking = FakeBooking();
        var payment = FakePayment("per-class", PaymentPricing.PerClassPrice);
        SetUpLookupsFor(booking, payment);

        string? sentHtml = null;
        _emailSender
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => sentHtml = html)
            .Returns(Task.CompletedTask);

        // Only AccountNumber is missing - still treated as fully
        // unconfigured, since showing two real values next to a blank
        // third one would be worse than showing none.
        var incompleteOptions = new PaymentOptions
        {
            AccountHolder = "Test Studio Holdings",
            Bank = "Test Bank",
            AccountNumber = null
        };

        var service = CreateService(incompleteOptions);

        // Must not throw - a missing bank account is a configuration gap,
        // not a reason to fail the whole booking confirmation.
        await service.SendConfirmationEmailAsync(booking);

        Assert.NotNull(sentHtml);
        Assert.Contains("The studio will send you payment details separately", sentHtml);
        Assert.DoesNotContain("Account holder:", sentHtml);
        Assert.DoesNotContain("Test Studio Holdings", sentHtml);
        Assert.DoesNotContain("123456789", sentHtml);
        Assert.DoesNotContain("Branch code", sentHtml);
    }

    [Fact]
    public async Task SendConfirmationEmailAsync_NeverIncludesTheOldHardcodedPlaceholderText()
    {
        var booking = FakeBooking();
        var payment = FakePayment("monthly", PaymentPricing.MonthlyPrice);
        SetUpLookupsFor(booking, payment);

        string? sentHtml = null;
        _emailSender
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, html) => sentHtml = html)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.SendConfirmationEmailAsync(booking);

        Assert.NotNull(sentHtml);
        Assert.DoesNotContain("123456789", sentHtml);
        Assert.DoesNotContain("Branch code 000000", sentHtml);
    }
}
