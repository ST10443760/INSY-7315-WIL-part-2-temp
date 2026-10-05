namespace ThriveWellness.Models
{
    // Entity: one row per studio venue a session can be held at.
    public class Location
    {
        public int LocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }
}
