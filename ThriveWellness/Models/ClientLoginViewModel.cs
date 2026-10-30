namespace ThriveWellness.Models
{
    // View model for the client sign-in page (/Account/ClientLogin).
    public class ClientLoginViewModel
    {
        // Already checked with Url.IsLocalUrl - null when there was none,
        // or when it pointed off-site.
        public string? ReturnUrl { get; set; }

        // False when Authentication:Google:ClientId/ClientSecret aren't
        // configured: the button is hidden and an explanation shown instead.
        public bool GoogleAvailable { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
