namespace ThriveWellness.Models
{
    public class Client
    {
        public int ClientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsNew { get; set; }
        public string PaymentType { get; set; } = string.Empty;
    }
}
