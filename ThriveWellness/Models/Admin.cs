namespace ThriveWellness.Models
{
    // Entity: one row per admin account - in practice there's only ever one,
    // since there's no sign-up flow. Created and kept in sync by
    // AdminSeeder.
    public class Admin
    {
        public int AdminId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        // Login lockout (see AuthService.Login): consecutive failed attempts
        // since the last success or lockout, and when the current lockout
        // ends (UTC). Null means not locked.
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }
}
