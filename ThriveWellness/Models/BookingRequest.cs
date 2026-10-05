namespace ThriveWellness.Models
{
    // Plain input to BookingService.CreateBookingAsync - the controller
    // builds this from BookingSubmitViewModel after MVC's own validation has
    // passed, so the service layer never has to depend on an
    // MVC-specific, annotated view model.
    public class BookingRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        // Billing plan for this booking's payment: "per-class" or "monthly".
        public string PaymentType { get; set; } = string.Empty;

        // Payment method for this booking's payment: "EFT" or "cash".
        public string Method { get; set; } = string.Empty;
        public int SessionId { get; set; }
        public string? MedicalNotes { get; set; }
        public bool ConsentSigned { get; set; }
    }
}
