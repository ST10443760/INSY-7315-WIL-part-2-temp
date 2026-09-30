namespace ThriveWellness.Models
{
    public class ClientOverviewViewModel
    {
        public int ClientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsNew { get; set; }
    }
}
