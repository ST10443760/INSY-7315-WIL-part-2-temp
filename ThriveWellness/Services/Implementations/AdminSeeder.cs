using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Runs once at startup (invoked from Program.cs) to make sure the single
    // admin account configured via Admin:Username/Admin:Password exists and
    // has the right password hash - this is how an admin account comes to
    // exist at all, since there's no sign-up flow (by design: this system
    // only ever has one admin account).
    public class AdminSeeder : IAdminSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminSeeder> _logger;

        public AdminSeeder(
            ApplicationDbContext context,
            IAuthService authService,
            IConfiguration configuration,
            ILogger<AdminSeeder> logger)
        {
            _context = context;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        // Creates the admin account if it doesn't exist yet, or brings its
        // password hash in line with configuration if it's drifted - safe to
        // call on every startup, including ones where nothing about the
        // admin account changed.
        public async Task SeedAsync()
        {
            // On Render, Admin__Username/Admin__Password (double underscore)
            // map onto these same Admin:Username/Admin:Password keys via
            // .NET's standard env-var configuration provider - no extra
            // wiring needed for that beyond reading IConfiguration here.
            var password = _configuration["Admin:Password"];
            if (string.IsNullOrEmpty(password))
            {
                _logger.LogWarning(
                    "Admin:Password is not configured - no admin account was created or updated. " +
                    "Set it (or Admin__Password on Render) to seed one.");
                return;
            }

            var username = _configuration["Admin:Username"];
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "admin";
            }

            var existing = await _context.Admins.FirstOrDefaultAsync(a => a.Username == username);

            if (existing == null)
            {
                _context.Admins.Add(new Admin
                {
                    Username = username,
                    PasswordHash = _authService.HashPassword(password)
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seeded admin account {Username}.", username);
                return;
            }

            // Re-hashing (BCrypt includes a random salt per call) on every
            // startup would mean every deploy looks like a password change
            // even when it isn't - only touch the row if the configured
            // password genuinely doesn't match what's already stored.
            if (!BCrypt.Net.BCrypt.Verify(password, existing.PasswordHash))
            {
                existing.PasswordHash = _authService.HashPassword(password);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Admin account {Username} already existed; updated its password hash.", username);
            }
        }
    }
}
