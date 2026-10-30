using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Implementations;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

// Wires the real BookingService, NotificationService and repositories
// together over an in-memory database, with only the email sender faked to
// fail - the same thing that happens when SendGrid rejects a send.
public class EmailFailureTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly BookingService _bookingService;

    public EmailFailureTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Locations.Add(new Location { LocationId = 1, Name = "Test Studio", Address = "Test Suburb" });
        _context.Sessions.Add(new Session
        {
            SessionId = 1,
            LocationId = 1,
            SessionType = "group",
            Date = DateTime.Today.AddDays(5),
            Time = new TimeSpan(9, 0, 0),
            Capacity = 10,
            IsOpen = true
        });
        // As created at Google sign-in: no phone yet, no bookings yet.
        _context.Clients.Add(new Client
        {
            ClientId = 1,
            GoogleSubject = "google-sub-1",
            FullName = "Jane Doe",
            Email = "jane@example.com",
            IsNew = true
        });
        _context.SaveChanges();

        _emailSender
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid returned 401"));

        var clientRepository = new ClientRepository(_context);
        var bookingRepository = new BookingRepository(_context);
        var sessionRepository = new SessionRepository(_context);
        var paymentRepository = new PaymentRepository(_context);

        var notificationService = new NotificationService(
            bookingRepository,
            clientRepository,
            sessionRepository,
            new LocationRepository(_context),
            paymentRepository,
            _emailSender.Object,
            _context,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AppBaseUrl"] = "https://test.local" }).Build(),
            Options.Create(new PaymentOptions()),
            NullLogger<NotificationService>.Instance);

        _bookingService = new BookingService(
            clientRepository,
            bookingRepository,
            sessionRepository,
            new Mock<IWaitlistService>().Object,
            paymentRepository,
            notificationService,
            new Mock<IRefundService>().Object,
            new LocationRepository(_context),
            TimeProvider.System,
            _context,
            NullLogger<BookingService>.Instance);
    }

    private static BookingRequest NewClientRequest() => new()
    {
        ClientId = 1,
        FullName = "Jane Doe",
        PhoneNumber = "082 123 4567",
        PaymentType = "per-class",
        Method = "EFT",
        SessionId = 1,
        ConsentSigned = true
    };

    [Fact]
    public async Task CreateBookingAsync_EmailFails_StillSucceedsAndKeepsTheBooking()
    {
        var result = await _bookingService.CreateBookingAsync(NewClientRequest());

        Assert.True(result.Success);
        var booking = await _context.Bookings.SingleAsync();
        Assert.Equal(result.BookingId, booking.BookingId);
        Assert.Equal("Awaiting Payment", booking.Status);
        Assert.Equal(1, await _context.Payments.CountAsync(p => p.BookingId == booking.BookingId));
        Assert.Equal(1, await _context.IntakeForms.CountAsync(i => i.BookingId == booking.BookingId));
    }

    [Fact]
    public async Task CreateBookingAsync_EmailFails_RecordsAFailedNotification()
    {
        var result = await _bookingService.CreateBookingAsync(NewClientRequest());

        var notification = await _context.Notifications.SingleAsync();
        Assert.Equal(result.BookingId, notification.BookingId);
        Assert.Equal("Confirmation", notification.Type);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_EmailSucceeds_RecordsASentNotification()
    {
        _emailSender
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _bookingService.CreateBookingAsync(NewClientRequest());

        var notification = await _context.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public async Task CancelByAdminAsync_EmailFails_StillCancelsAndRecordsAFailedNotification()
    {
        var created = await _bookingService.CreateBookingAsync(NewClientRequest());

        var result = await _bookingService.CancelByAdminAsync(created.BookingId!.Value);

        Assert.True(result.Success);
        Assert.Equal("Cancelled", (await _context.Bookings.SingleAsync()).Status);
        Assert.Equal(NotificationStatus.Failed,
            (await _context.Notifications.SingleAsync(n => n.Type == "Cancellation")).Status);
    }
}
