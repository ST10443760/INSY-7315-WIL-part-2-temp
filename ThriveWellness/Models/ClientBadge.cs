namespace ThriveWellness.Models
{
    // Entity: a badge a client has earned. One row per client per badge
    // (unique), written by RewardsService and never deleted.
    public class ClientBadge
    {
        public int ClientBadgeId { get; set; }
        public int ClientId { get; set; }
        public int BadgeId { get; set; }
        public DateTime AwardedAt { get; set; }
    }
}
