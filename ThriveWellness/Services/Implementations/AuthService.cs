using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: the two BCrypt operations behind admin login -
    // verifying a plaintext password against a stored hash, and hashing a
    // new one. Deliberately thin; AdminSeeder and the login controller both
    // depend on this rather than calling BCrypt directly, so there's exactly
    // one place that could switch hashing algorithms later.
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Verifies a login attempt: looks the admin up by username, then
        // uses BCrypt's own constant-time comparison (Verify) rather than
        // hashing the attempt and comparing strings, which would leak timing
        // information. Returns false for both "no such admin" and "wrong
        // password" - the caller never learns which, which is what stops
        // this being a username-enumeration oracle.
        public async Task<bool> Login(string username, string password)
        {
            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Username == username);

            if (admin == null)
            {
                return false;
            }

            return BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash);
        }

        // Hashes a plaintext password with BCrypt, which generates and
        // embeds its own random salt - used both for the initial admin seed
        // and whenever AdminSeeder needs to replace an out-of-date hash.
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}
