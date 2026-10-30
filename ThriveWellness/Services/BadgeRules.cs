using ThriveWellness.Models;

namespace ThriveWellness.Services
{
    // What a client has done, as far as badges care - worked out from their
    // attended classes only (see Attendance).
    public class RewardsStats
    {
        public int AttendedCount { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public bool AttendedThisWeek { get; set; }

        // How many different studios they've attended a class at.
        public int StudioCount { get; set; }

        // Attended classes that start before EarlyBirdBefore.
        public int EarlyClassCount { get; set; }
    }

    // What earns each seeded badge (see BadgeCodes), and what to say about
    // the next one. Pure, so the rules are tested without a database.
    public static class BadgeRules
    {
        public static readonly IReadOnlyList<(string Code, int Classes)> ClassMilestones = new[]
        {
            (BadgeCodes.FirstClass, 1),
            (BadgeCodes.GettingGoing, 5),
            (BadgeCodes.Committed, 10),
            (BadgeCodes.Dedicated, 25)
        };

        public static readonly IReadOnlyList<(string Code, int Weeks)> StreakMilestones = new[]
        {
            (BadgeCodes.ThreeWeekStreak, 3),
            (BadgeCodes.EightWeekStreak, 8)
        };

        public const int StudiosForBothStudios = 2;
        public const int EarlyClassesForEarlyBird = 3;
        public static readonly TimeSpan EarlyBirdBefore = new(9, 0, 0);

        // Every badge these stats qualify for. A streak badge goes by the
        // longest streak, so it's earned the moment the run is reached.
        public static IReadOnlyList<string> EarnedCodes(RewardsStats stats)
        {
            var codes = new List<string>();
            codes.AddRange(ClassMilestones.Where(m => stats.AttendedCount >= m.Classes).Select(m => m.Code));
            codes.AddRange(StreakMilestones.Where(m => stats.LongestStreak >= m.Weeks).Select(m => m.Code));
            if (stats.StudioCount >= StudiosForBothStudios)
            {
                codes.Add(BadgeCodes.BothStudios);
            }
            if (stats.EarlyClassCount >= EarlyClassesForEarlyBird)
            {
                codes.Add(BadgeCodes.EarlyBird);
            }
            return codes;
        }

        // "3 more classes to Committed" for the next class milestone not yet
        // earned; once those are all earned, the next streak milestone
        // instead. Null when there's nothing left to work towards.
        public static string? NextBadgeText(RewardsStats stats, ISet<string> earnedCodes, IReadOnlyDictionary<string, string> badgeNames)
        {
            foreach (var (code, classes) in ClassMilestones)
            {
                if (!earnedCodes.Contains(code) && badgeNames.TryGetValue(code, out var name))
                {
                    var left = Math.Max(1, classes - stats.AttendedCount);
                    return $"{left} more {(left == 1 ? "class" : "classes")} to {name}";
                }
            }

            foreach (var (code, weeks) in StreakMilestones)
            {
                if (!earnedCodes.Contains(code) && badgeNames.TryGetValue(code, out var name))
                {
                    var left = Math.Max(1, weeks - stats.CurrentStreak);
                    return $"{left} more {(left == 1 ? "week" : "weeks")} in a row to {name}";
                }
            }

            return null;
        }

        // The one line under the streak.
        public static string Encouragement(RewardsStats stats)
        {
            if (stats.AttendedCount == 0)
            {
                return "Your first class starts your streak.";
            }
            if (stats.CurrentStreak == 0)
            {
                return "Book a class this week to start a new streak.";
            }
            if (!stats.AttendedThisWeek)
            {
                return "Come to a class this week to keep your streak going.";
            }
            return stats.CurrentStreak == 1
                ? "Great start - come back next week to make it two."
                : $"{stats.CurrentStreak} weeks in a row - keep it going!";
        }
    }
}
