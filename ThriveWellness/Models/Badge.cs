namespace ThriveWellness.Models
{
    // Entity: one reward a client can earn, seeded by migration (see
    // ApplicationDbContext). What it takes to earn each one lives in
    // BadgeRules, keyed by Code.
    public class Badge
    {
        public int BadgeId { get; set; }

        // Stable key, unique, for example "first-class".
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Which inline SVG icon the dashboard draws for it.
        public string IconKey { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }
}
