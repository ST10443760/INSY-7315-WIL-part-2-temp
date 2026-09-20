namespace ThriveWellness.Models
{
    public class WaitlistEntryViewModel
    {
        public int Position { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public DateTime DateAdded { get; set; }
    }
}
