namespace ThriveWellness.Models
{
    // The Rewards section of the client dashboard: streak, next badge and
    // every badge, earned or locked. Built by RewardsService.
    public class RewardsViewModel
    {
        public int AttendedCount { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public string Encouragement { get; set; } = string.Empty;

        // "3 more classes to Committed"; null once there's nothing left.
        public string? NextBadgeText { get; set; }
        public IReadOnlyList<BadgeViewModel> Badges { get; set; } = Array.Empty<BadgeViewModel>();

        public bool HasAttended => AttendedCount > 0;
        public int EarnedCount => Badges.Count(b => b.Earned);
    }

    // One client's rewards in brief, for the admin Clients page.
    public record ClientRewardsSummary(int AttendedCount, int CurrentStreak, int BadgeCount);

    public class BadgeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool Earned { get; set; }
        public DateTime? AwardedAt { get; set; }

        // Awarded in the last 7 days.
        public bool IsNew { get; set; }
    }
}
