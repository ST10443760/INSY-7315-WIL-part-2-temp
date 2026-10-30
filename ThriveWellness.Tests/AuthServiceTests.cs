using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class AuthServiceTests
{
    // A clock the tests move by hand, so the 15 minute lockout can be
    // checked without waiting for it.
    private class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private const string Username = "admin";
    private const string CorrectPassword = "correct-password";
    private const string WrongPassword = "wrong-password";

    private readonly FakeClock _clock = new();
    private readonly ApplicationDbContext _context;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Low work factor only to keep the tests quick - the lockout logic
        // doesn't depend on it.
        _context.Admins.Add(new Admin { Username = Username, PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword, 4) });
        _context.SaveChanges();

        _service = new AuthService(_context, _clock, new MemoryCache(new MemoryCacheOptions()));
    }

    private async Task<List<LoginResult>> FailTimes(string username, int times)
    {
        var results = new List<LoginResult>();
        for (var i = 0; i < times; i++)
        {
            results.Add(await _service.Login(username, WrongPassword));
        }
        return results;
    }

    private Admin StoredAdmin() => _context.Admins.Single(a => a.Username == Username);

    [Fact]
    public async Task Login_CorrectPassword_Succeeds()
    {
        Assert.Equal(LoginResult.Success, await _service.Login(Username, CorrectPassword));
    }

    [Fact]
    public async Task Login_FiveFailures_LockTheAccount()
    {
        var results = await FailTimes(Username, 5);

        Assert.All(results.Take(4), r => Assert.Equal(LoginResult.InvalidCredentials, r));
        Assert.Equal(LoginResult.LockedOut, results[4]);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime + AuthService.LockoutDuration, StoredAdmin().LockoutEnd);
    }

    [Fact]
    public async Task Login_LockedAccount_RejectsEvenTheCorrectPassword()
    {
        await FailTimes(Username, 5);

        Assert.Equal(LoginResult.LockedOut, await _service.Login(Username, CorrectPassword));
    }

    [Fact]
    public async Task Login_LockedAccount_UnlocksAfterFifteenMinutes()
    {
        await FailTimes(Username, 5);

        _clock.Advance(TimeSpan.FromMinutes(14).Add(TimeSpan.FromSeconds(59)));
        Assert.Equal(LoginResult.LockedOut, await _service.Login(Username, CorrectPassword));

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(LoginResult.Success, await _service.Login(Username, CorrectPassword));
        Assert.Null(StoredAdmin().LockoutEnd);
        Assert.Equal(0, StoredAdmin().FailedLoginCount);
    }

    [Fact]
    public async Task Login_AfterALockoutExpires_OneMoreFailureDoesNotLockAgain()
    {
        await FailTimes(Username, 5);
        _clock.Advance(AuthService.LockoutDuration);

        Assert.Equal(LoginResult.InvalidCredentials, await _service.Login(Username, WrongPassword));
        Assert.Equal(1, StoredAdmin().FailedLoginCount);
    }

    [Fact]
    public async Task Login_Success_ResetsTheFailedCount()
    {
        await FailTimes(Username, 4);
        Assert.Equal(4, StoredAdmin().FailedLoginCount);

        Assert.Equal(LoginResult.Success, await _service.Login(Username, CorrectPassword));
        Assert.Equal(0, StoredAdmin().FailedLoginCount);

        // Four more failures would have locked the account if the earlier
        // four had still counted.
        var results = await FailTimes(Username, 4);
        Assert.All(results, r => Assert.Equal(LoginResult.InvalidCredentials, r));
    }

    [Fact]
    public async Task Login_UnknownUsername_GetsExactlyTheSameResponsesAsARealOne()
    {
        var realUser = await FailTimes(Username, 6);
        var unknownUser = await FailTimes("not-an-admin", 6);

        Assert.Equal(realUser, unknownUser);
        Assert.Equal(LoginResult.InvalidCredentials, unknownUser[0]);
        Assert.Equal(LoginResult.LockedOut, unknownUser[5]);
    }

    [Fact]
    public async Task Login_UnknownUsername_UnlocksAfterFifteenMinutesToo()
    {
        await FailTimes("not-an-admin", 5);

        _clock.Advance(AuthService.LockoutDuration);

        Assert.Equal(LoginResult.InvalidCredentials, await _service.Login("not-an-admin", WrongPassword));
    }

    [Fact]
    public async Task Login_UnknownUsername_NeverCreatesAnAdminRow()
    {
        await FailTimes("not-an-admin", 5);

        Assert.Equal(1, await _context.Admins.CountAsync());
    }
}
