using System.ComponentModel.DataAnnotations;

namespace ThriveWellness.Models
{
    public class BookingSubmitViewModel
    {
        [Required]
        public int SessionId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        // Only required for a new client — an existing client's payment
        // type on file is unaffected by this booking, so it's validated
        // conditionally in the controller rather than with [Required] here.
        public string PaymentType { get; set; } = string.Empty;

        public bool IsNewClient { get; set; }

        public string? MedicalNotes { get; set; }

        public bool ConsentSigned { get; set; }

        // Display-only, repopulated by the controller on every render.
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
    }
}
