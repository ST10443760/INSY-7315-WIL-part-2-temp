using Microsoft.EntityFrameworkCore;
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
        }
    }
}
