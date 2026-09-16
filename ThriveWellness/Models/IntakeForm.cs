namespace ThriveWellness.Models
{
    public class IntakeForm
    {
        public int IntakeFormId { get; set; }
        public int BookingId { get; set; }
        public string MedicalNotes { get; set; } = string.Empty;
        public bool ConsentSigned { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}
