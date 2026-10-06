namespace ThriveWellness.Models
{
    // Return value of WaitlistService.AddToClassAsync.
    public class AddToClassResult
    {
        public bool Success { get; set; }

        // Set when Success is false - shown to the admin as an error banner,
        // no changes made.
        public string? ErrorMessage { get; set; }

        // Only meaningful when Success is true: the booking and its Pending
        // payment were created and committed either way - this only reflects
        // whether the follow-up payment-details email actually went out.
        public bool EmailSent { get; set; } = true;
    }
}
