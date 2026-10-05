namespace ThriveWellness.Models
{
    // Return value of BookingService.CheckClientStatusAsync - tells the
    // booking flow's email step whether to show the full intake form or just
    // confirm the existing details on file.
    public class ClientStatusResult
    {
        public bool IsNew { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
