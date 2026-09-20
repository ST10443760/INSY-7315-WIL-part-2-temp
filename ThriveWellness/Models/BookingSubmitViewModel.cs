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

        // Billing plan for this booking's payment: "per-class" or "monthly"
        // (drives the Payment amount, FR-06). Needed for every booking
        // regardless of new/returning client, so it's validated in the
        // controller rather than with [Required] here (keeps this model
        // usable for both the new-client and returning-client form states).
        public string PaymentType { get; set; } = string.Empty;

        // Payment method for this booking's payment: "EFT" or "cash".
        public string Method { get; set; } = string.Empty;

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
