using Moq;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class SessionServiceTests
{
    private readonly Mock<ISessionRepository> _sessionRepository = new();
    private readonly Mock<IWaitlistService> _waitlistService = new();

    private SessionService CreateService() => new(_sessionRepository.Object, _waitlistService.Object);

    private static Session ValidSession(int capacity = 10, DateTime? date = null) => new()
    {
        LocationId = 1,
        SessionType = "group",
        Date = date ?? DateTime.Today.AddDays(1),
        Time = new TimeSpan(9, 0, 0),
        Capacity = capacity,
        IsOpen = true
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAsync_CapacityNotPositive_IsRejected(int capacity)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(ValidSession(capacity: capacity)));

        _sessionRepository.Verify(r => r.AddAsync(It.IsAny<Session>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PastDate_IsRejected()
    {
        var service = CreateService();
        var pastSession = ValidSession(date: DateTime.Today.AddDays(-1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(pastSession));

        Assert.Contains("past", ex.Message, StringComparison.OrdinalIgnoreCase);
        _sessionRepository.Verify(r => r.AddAsync(It.IsAny<Session>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidSession_IsCreated()
    {
        _sessionRepository.Setup(r => r.AddAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);
        var service = CreateService();
        var session = ValidSession();

        await service.CreateAsync(session);

        _sessionRepository.Verify(r => r.AddAsync(session), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_TodayAsDate_IsAllowed()
    {
        // The past-date check is session.Date.Date < DateTime.Today, so
        // today itself is a valid, bookable session date, not "the past".
        _sessionRepository.Setup(r => r.AddAsync(It.IsAny<Session>())).Returns(Task.CompletedTask);
        var service = CreateService();
        var session = ValidSession(date: DateTime.Today);

        await service.CreateAsync(session);

        _sessionRepository.Verify(r => r.AddAsync(session), Times.Once);
    }
}
