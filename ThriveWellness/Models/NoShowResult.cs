namespace ThriveWellness.Models
{
    // Return value of BookingService.SetNoShowAsync.
    public class NoShowResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        // Set whenever the booking was found, so the caller can go back to
        // its session and re-check the client's rewards.
        public int? ClientId { get; set; }
        public int? SessionId { get; set; }
    }
}
