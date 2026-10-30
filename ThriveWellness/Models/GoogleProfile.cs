namespace ThriveWellness.Models
{
    // What ClientAccountService needs from a completed Google sign-in -
    // AccountController reads these off Google's claims, so the service
    // itself never depends on ASP.NET Core's authentication types.
    public class GoogleProfile
    {
        // Google's stable account id (the "sub" claim).
        public string Subject { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // Return value of ClientAccountService.SignInWithGoogleAsync.
    public class ClientSignInResult
    {
        public bool Success { get; set; }

        // Set when Success is true.
        public Client? Client { get; set; }

        // Set when Success is false - safe to show to the client.
        public string? ErrorMessage { get; set; }
    }
}
