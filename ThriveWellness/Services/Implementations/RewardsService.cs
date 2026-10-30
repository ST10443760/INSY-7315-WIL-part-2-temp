using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Services.Implementations
{
    // Service layer: client rewards. Works out a client's attended classes
    // (the shared Attendance rule), their weekly streak (Streaks) and which
    // badges that earns (BadgeRules), and records newly earned badges.
    //
    // Badges, once earned, are never removed - not even if a booking that
    // earned one is later cancelled, refunded or marked a no-show. Taking a
    // reward back would feel like a punishment, so EvaluateAsync only ever
    // adds.
    public class RewardsService : IRewardsService
    {
        private const int NewBadgeDays = 7;

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RewardsService> _logger;

        public RewardsService(ApplicationDbContext context, TimeProvider timeProvider, ILogger<RewardsService> logger)
        {
            _context = context;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> EvaluateAsync(int clientId)
        {
            var stats = await LoadStatsAsync(clientId);
            var earnedCodes = BadgeRules.EarnedCodes(stats);
            if (earnedCodes.Count == 0)
            {
                return Array.Empty<string>();
            }

            var alreadyHeld = await _context.ClientBadges
                .Where(cb => cb.ClientId == clientId)
                .Select(cb => cb.BadgeId)
                .ToListAsync();

            var toAward = await _context.Badges
                .Where(b => earnedCodes.Contains(b.Code) && !alreadyHeld.Contains(b.BadgeId))
                .ToListAsync();
            if (toAward.Count == 0)
            {
                return Array.Empty<string>();
            }

            var now = DateTime.UtcNow;
            foreach (var badge in toAward)
            {
                _context.ClientBadges.Add(new ClientBadge { ClientId = clientId, BadgeId = badge.BadgeId, AwardedAt = now });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two requests evaluating the same client at once: the
                // unique (ClientId, BadgeId) index stopped the duplicate, and
                // the other request has already awarded it.
                foreach (var entry in _context.ChangeTracker.Entries<ClientBadge>().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }
                return Array.Empty<string>();
            }

            _logger.LogInformation("Client {ClientId} earned {Badges}.", clientId, string.Join(", ", toAward.Select(b => b.Code)));
            return toAward.Select(b => b.Code).ToList();
        }

        public async Task<RewardsViewModel> GetRewardsAsync(int clientId)
        {
            var stats = await LoadStatsAsync(clientId);
            var badges = await _context.Badges.OrderBy(b => b.SortOrder).ToListAsync();
            var awards = await _context.ClientBadges
                .Where(cb => cb.ClientId == clientId)
                .ToDictionaryAsync(cb => cb.BadgeId, cb => cb.AwardedAt);

            var newSince = DateTime.UtcNow.AddDays(-NewBadgeDays);
            var earnedCodes = badges.Where(b => awards.ContainsKey(b.BadgeId)).Select(b => b.Code).ToHashSet();

            return new RewardsViewModel
            {
                AttendedCount = stats.AttendedCount,
                CurrentStreak = stats.CurrentStreak,
                LongestStreak = stats.LongestStreak,
                Encouragement = BadgeRules.Encouragement(stats),
                NextBadgeText = BadgeRules.NextBadgeText(stats, earnedCodes, badges.ToDictionary(b => b.Code, b => b.Name)),
                Badges = badges.Select(b => new BadgeViewModel
                {
                    Code = b.Code,
                    Name = b.Name,
                    Description = b.Description,
                    IconKey = b.IconKey,
                    Earned = awards.ContainsKey(b.BadgeId),
                    AwardedAt = awards.TryGetValue(b.BadgeId, out var at) ? at : null,
                    IsNew = awards.TryGetValue(b.BadgeId, out var awardedAt) && awardedAt >= newSince
                }).ToList()
            };
        }

        public async Task<IReadOnlyDictionary<int, ClientRewardsSummary>> GetSummariesAsync()
        {
            var now = _timeProvider.GetLocalNow().DateTime;
            var bookingsByClient = (await AttendanceRows(null).ToListAsync()).GroupBy(b => b.ClientId);
            var badgeCounts = await _context.ClientBadges
                .GroupBy(cb => cb.ClientId)
                .Select(g => new { ClientId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ClientId, x => x.Count);

            var summaries = new Dictionary<int, ClientRewardsSummary>();
            foreach (var group in bookingsByClient)
            {
                var stats = BuildStats(group, now);
                summaries[group.Key] = new ClientRewardsSummary(stats.AttendedCount, stats.CurrentStreak, badgeCounts.GetValueOrDefault(group.Key));
            }
            foreach (var (clientId, count) in badgeCounts.Where(b => !summaries.ContainsKey(b.Key)))
            {
                summaries[clientId] = new ClientRewardsSummary(0, 0, count);
            }
            return summaries;
        }

        private async Task<RewardsStats> LoadStatsAsync(int clientId)
        {
            var bookings = await AttendanceRows(clientId).ToListAsync();
            return BuildStats(bookings, _timeProvider.GetLocalNow().DateTime);
        }

        // Every booking (for one client, or everyone) with what the
        // attendance rule needs.
        private IQueryable<AttendanceRow> AttendanceRows(int? clientId)
        {
            return
                from booking in _context.Bookings
                join session in _context.Sessions on booking.SessionId equals session.SessionId
                join payment in _context.Payments on booking.BookingId equals payment.BookingId into payments
                from payment in payments.DefaultIfEmpty()
                where clientId == null || booking.ClientId == clientId
                select new AttendanceRow(
                    booking.ClientId, booking.Status, booking.NoShow,
                    payment != null ? payment.Status : null,
                    session.Date, session.Time, session.LocationId);
        }

        // One booking with what the attendance rule needs.
        public record AttendanceRow(
            int ClientId, string BookingStatus, bool NoShow, string? PaymentStatus,
            DateTime SessionDate, TimeSpan SessionTime, int LocationId);

        public static RewardsStats BuildStats(IEnumerable<AttendanceRow> bookings, DateTime now)
        {
            var attended = bookings
                .Where(b => Attendance.IsAttended(b.BookingStatus, b.NoShow, b.PaymentStatus, b.SessionDate.Date + b.SessionTime, now))
                .ToList();

            var streak = Streaks.Calculate(attended.Select(b => b.SessionDate), now.Date);

            return new RewardsStats
            {
                AttendedCount = attended.Count,
                CurrentStreak = streak.Current,
                LongestStreak = streak.Longest,
                AttendedThisWeek = streak.AttendedThisWeek,
                StudioCount = attended.Select(b => b.LocationId).Distinct().Count(),
                EarlyClassCount = attended.Count(b => b.SessionTime < BadgeRules.EarlyBirdBefore)
            };
        }
    }
}
