using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    // The direct "join the waitlist without booking" entry point for a
    // session that's already full or closed - lighter-weight than the full
    // booking flow, no payment fields.
    public class WaitlistJoinViewModel
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Display-only, repopulated by the controller on every render.
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
    }
}
