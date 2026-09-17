using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
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
    }
}
