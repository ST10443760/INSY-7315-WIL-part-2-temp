using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    // View model for the booking flow's first step - just a session and an
    // email, enough for CheckClientStatusAsync to decide whether the next
    // step shows the full intake form or just a confirmation.
    public class BookingEmailStepViewModel
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Display-only, populated by the controller.
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public string LocationAddress { get; set; } = string.Empty;
    }
}
