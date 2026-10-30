using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: the two BCrypt operations behind admin login -
    // verifying a plaintext password against a stored hash, and hashing a
    // new one - plus the login lockout. AdminSeeder and the login controller
    // both depend on this rather than calling BCrypt directly, so there's
    // exactly one place that could switch hashing algorithms later.
    public class AuthService : IAuthService
    {
        public const int MaxFailedAttempts = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        // A real BCrypt hash of a random throwaway value, made once per
        // process with the same default work factor HashPassword uses, so
        // checking a password against it for an unknown username takes as
        // long as checking a real admin's password.
        private static readonly Lazy<string> DummyHash =
            new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()));

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly IMemoryCache _cache;

        public AuthService(ApplicationDbContext context, TimeProvider timeProvider, IMemoryCache cache)
        {
            _context = context;
            _timeProvider = timeProvider;
            _cache = cache;
        }

        // Verifies a login attempt: looks the admin up by username, then
        // uses BCrypt's own constant-time comparison (Verify) rather than
        // hashing the attempt and comparing strings, which would leak timing
        // information.
        //
        // Lockout: MaxFailedAttempts consecutive failures lock the account
        // for LockoutDuration, during which even the right password is
        // refused; a success resets the count. An unknown username behaves
        // identically from the outside - it still pays for a BCrypt verify
        // (against DummyHash) and still locks after the same number of
        // tries, with its count kept in memory since there's no row to keep
        // it on - so neither the response nor its timing reveals whether
        // the username exists.
        public async Task<LoginResult> Login(string username, string password)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Username == username);

            if (admin == null)
            {
                BCrypt.Net.BCrypt.Verify(password, DummyHash.Value);
                return RecordUnknownUserFailure(username, now);
            }

            // Verified before the lockout check, so a locked account costs
            // the same time as an unlocked one.
            var passwordMatches = BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);

            if (IsLockedOut(admin.LockoutEnd, now))
            {
                return LoginResult.LockedOut;
            }

            if (passwordMatches)
            {
                admin.FailedLoginCount = 0;
                admin.LockoutEnd = null;
                // A no-op (no database round trip) when nothing changed.
                await _context.SaveChangesAsync();
                return LoginResult.Success;
            }

            var (count, lockoutEnd, result) = AfterFailure(admin.FailedLoginCount, admin.LockoutEnd, now);
            admin.FailedLoginCount = count;
            admin.LockoutEnd = lockoutEnd;
            await _context.SaveChangesAsync();
            return result;
        }

        // Hashes a plaintext password with BCrypt, which generates and
        // embeds its own random salt - used both for the initial admin seed
        // and whenever AdminSeeder needs to replace an out-of-date hash.
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private LoginResult RecordUnknownUserFailure(string username, DateTime now)
        {
            var attempts = _cache.GetOrCreate($"login-failures:{username}", entry =>
            {
                // Only here to stop abandoned entries piling up; the lockout
                // itself is timed by LockoutEnd, not by this expiry.
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                return new FailedAttempts();
            })!;

            lock (attempts)
            {
                if (IsLockedOut(attempts.LockoutEnd, now))
                {
                    return LoginResult.LockedOut;
                }

                var (count, lockoutEnd, result) = AfterFailure(attempts.Count, attempts.LockoutEnd, now);
                attempts.Count = count;
                attempts.LockoutEnd = lockoutEnd;
                return result;
            }
        }

        private static bool IsLockedOut(DateTime? lockoutEnd, DateTime now) =>
            lockoutEnd.HasValue && lockoutEnd.Value > now;

        // The one rule both paths share for a failed attempt: an expired
        // lockout starts the count again from zero, and the attempt that
        // reaches MaxFailedAttempts starts a new lockout (and is itself
        // answered with LockedOut).
        private static (int Count, DateTime? LockoutEnd, LoginResult Result) AfterFailure(
            int count, DateTime? lockoutEnd, DateTime now)
        {
            if (lockoutEnd.HasValue)
            {
                count = 0;
            }

            count++;
            if (count >= MaxFailedAttempts)
            {
                return (0, now + LockoutDuration, LoginResult.LockedOut);
            }

            return (count, null, LoginResult.InvalidCredentials);
        }

        private class FailedAttempts
        {
            public int Count { get; set; }
            public DateTime? LockoutEnd { get; set; }
        }
    }
}
