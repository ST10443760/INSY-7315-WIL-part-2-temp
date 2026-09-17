using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ThriveWellness.Models;

namespace ThriveWellness.Data
{
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
        public DbSet<Waitlist> Waitlists => Set<Waitlist>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<IntakeForm> IntakeForms => Set<IntakeForm>();
        public DbSet<Admin> Admins => Set<Admin>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

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
