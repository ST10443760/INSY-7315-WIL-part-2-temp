using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ThriveWellness.Models;

namespace ThriveWellness.Data
{
    // EF Core's entry point to the database: the DbSets every repository
    // queries against, plus (in OnModelCreating below) the relationship
    // configuration, seeded location data, and the DateTime handling that
    // Npgsql needs. Registered once in Program.cs and injected as a Scoped
    // service - one instance per HTTP request, shared by every repository
    // that request touches.
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Location> Locations => Set<Location>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<Waitlist> Waitlists => Set<Waitlist>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<IntakeForm> IntakeForms => Set<IntakeForm>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Badge> Badges => Set<Badge>();
        public DbSet<ClientBadge> ClientBadges => Set<ClientBadge>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Every relationship below is spelled out explicitly rather than
            // left to EF Core's own convention-based discovery, and every
            // one restricts deletes except the true child records (a
            // booking's Payment, IntakeForm and Notifications, which only
            // exist because that booking does) - deleting a Client, Session
            // or Booking that still has dependent rows fails loudly instead
            // of quietly cascading data loss through the booking history.

            // Location 1:many Session
            modelBuilder.Entity<Session>()
                .HasOne<Location>()
                .WithMany()
                .HasForeignKey(s => s.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Client 1:many Booking
            modelBuilder.Entity<Booking>()
                .HasOne<Client>()
                .WithMany()
                .HasForeignKey(b => b.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Session 1:many Booking
            modelBuilder.Entity<Booking>()
                .HasOne<Session>()
                .WithMany()
                .HasForeignKey(b => b.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Booking 1:1 Payment
            modelBuilder.Entity<Payment>()
                .HasOne<Booking>()
                .WithOne()
                .HasForeignKey<Payment>(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Payment 1:many PaymentTransaction (a decline, then a retry)
            modelBuilder.Entity<PaymentTransaction>()
                .HasOne<Payment>()
                .WithMany()
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Booking 1:0..1 IntakeForm
            modelBuilder.Entity<IntakeForm>()
                .HasOne<Booking>()
                .WithOne()
                .HasForeignKey<IntakeForm>(i => i.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Client 1:many Waitlist
            modelBuilder.Entity<Waitlist>()
                .HasOne<Client>()
                .WithMany()
                .HasForeignKey(w => w.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Session 1:many Waitlist
            modelBuilder.Entity<Waitlist>()
                .HasOne<Session>()
                .WithMany()
                .HasForeignKey(w => w.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Booking 1:many Notification
            modelBuilder.Entity<Notification>()
                .HasOne<Booking>()
                .WithMany()
                .HasForeignKey(n => n.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // One client row per Google account. Filtered so the many
            // clients who haven't signed in yet (null) don't clash.
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.GoogleSubject)
                .IsUnique()
                .HasFilter("\"GoogleSubject\" IS NOT NULL");

            // Every row logged before Status existed was a successful send,
            // so the column defaults to Sent for them.
            modelBuilder.Entity<Notification>()
                .Property(n => n.Status)
                .HasDefaultValue(NotificationStatus.Sent);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PaymentTransaction>()
                .Property(t => t.Amount)
                .HasColumnType("decimal(18,2)");

            // Client 1:many ClientBadge, Badge 1:many ClientBadge. A client
            // with badges can't be deleted (same rule as their bookings); a
            // badge definition can, taking its awards with it.
            modelBuilder.Entity<ClientBadge>()
                .HasOne<Client>()
                .WithMany()
                .HasForeignKey(cb => cb.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClientBadge>()
                .HasOne<Badge>()
                .WithMany()
                .HasForeignKey(cb => cb.BadgeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Each badge at most once per client - the database backs up
            // RewardsService's own "never award twice" check.
            modelBuilder.Entity<ClientBadge>()
                .HasIndex(cb => new { cb.ClientId, cb.BadgeId })
                .IsUnique();

            modelBuilder.Entity<Badge>()
                .HasIndex(b => b.Code)
                .IsUnique();

            // The badges clients can earn, seeded via migration so the live
            // database gets them too. What earns each one is in BadgeRules.
            modelBuilder.Entity<Badge>().HasData(
                new Badge { BadgeId = 1, Code = BadgeCodes.FirstClass, Name = "First Class", Description = "Attended your first class.", IconKey = "star", SortOrder = 1 },
                new Badge { BadgeId = 2, Code = BadgeCodes.GettingGoing, Name = "Getting Going", Description = "Attended 5 classes.", IconKey = "leaf", SortOrder = 2 },
                new Badge { BadgeId = 3, Code = BadgeCodes.Committed, Name = "Committed", Description = "Attended 10 classes.", IconKey = "medal", SortOrder = 3 },
                new Badge { BadgeId = 4, Code = BadgeCodes.Dedicated, Name = "Dedicated", Description = "Attended 25 classes.", IconKey = "trophy", SortOrder = 4 },
                new Badge { BadgeId = 5, Code = BadgeCodes.ThreeWeekStreak, Name = "Three Week Streak", Description = "Attended a class in 3 weeks in a row.", IconKey = "flame", SortOrder = 5 },
                new Badge { BadgeId = 6, Code = BadgeCodes.EightWeekStreak, Name = "Eight Week Streak", Description = "Attended a class in 8 weeks in a row.", IconKey = "bolt", SortOrder = 6 },
                new Badge { BadgeId = 7, Code = BadgeCodes.BothStudios, Name = "Both Studios", Description = "Attended a class at both of our studios.", IconKey = "pin", SortOrder = 7 },
                new Badge { BadgeId = 8, Code = BadgeCodes.EarlyBird, Name = "Early Bird", Description = "Attended 3 classes that start before 09:00.", IconKey = "sunrise", SortOrder = 8 }
            );

            // Known studio locations, seeded via migration.
            modelBuilder.Entity<Location>().HasData(
                new Location { LocationId = 1, Name = "The Hub, Little Village", Address = "Kyalami" },
                new Location { LocationId = 2, Name = "Katz World of Dance, Studio 2", Address = "Sunninghill" }
            );

            // None of the DateTime columns in this schema carry real timezone
            // semantics (session date, booking date, etc. are naive/local),
            // so map them all to "timestamp without time zone". Npgsql is
            // strict about DateTime.Kind matching the column type — it
            // rejects Kind=Utc here just as it would reject Kind=Unspecified
            // against timestamptz — so every write is normalized to
            // Unspecified regardless of how the value was constructed
            // (DateTime.Now, DateTime.UtcNow, or model-bound form input).
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
                v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetColumnType("timestamp without time zone");
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetColumnType("timestamp without time zone");
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }
}
