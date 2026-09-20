namespace ThriveWellness.Models
{
    public class WaitlistJoinedViewModel
    {
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionTime { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int Position { get; set; }
    }
}
