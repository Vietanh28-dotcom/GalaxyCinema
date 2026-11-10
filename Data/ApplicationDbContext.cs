using GalaxyCinema.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingSeat> BookingSeats => Set<BookingSeat>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<SeatLock> SeatLocks => Set<SeatLock>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<MovieRating> MovieRatings => Set<MovieRating>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BookingSeat>().HasKey(bs => new { bs.BookingId, bs.SeatId });

        builder.Entity<BookingSeat>()
            .HasOne(bs => bs.Booking)
            .WithMany(b => b.BookingSeats)
            .HasForeignKey(bs => bs.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BookingSeat>()
            .HasOne(bs => bs.Seat)
            .WithMany(s => s.BookingSeats)
            .HasForeignKey(bs => bs.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Room>()
            .HasOne(r => r.Cinema)
            .WithMany(c => c.Rooms)
            .HasForeignKey(r => r.CinemaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Seat>()
            .HasOne(s => s.Room)
            .WithMany(r => r.Seats)
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Seat>()
            .HasIndex(s => new { s.RoomId, s.Code })
            .IsUnique();

        builder.Entity<Showtime>()
            .HasOne(st => st.Movie)
            .WithMany(m => m.Showtimes)
            .HasForeignKey(st => st.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Showtime>()
            .HasOne(st => st.Room)
            .WithMany(r => r.Showtimes)
            .HasForeignKey(st => st.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Showtime>().HasIndex(st => st.StartTime);

        builder.Entity<Booking>()
            .HasOne(b => b.Showtime)
            .WithMany(st => st.Bookings)
            .HasForeignKey(b => b.ShowtimeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>().HasIndex(b => b.BookingCode).IsUnique();
        builder.Entity<Booking>().HasIndex(b => b.CreatedAt);

        builder.Entity<SeatLock>()
            .HasOne(l => l.Seat)
            .WithMany(s => s.Locks)
            .HasForeignKey(l => l.SeatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SeatLock>()
            .HasOne(l => l.Showtime)
            .WithMany()
            .HasForeignKey(l => l.ShowtimeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SeatLock>()
            .HasIndex(l => new { l.ShowtimeId, l.SeatId })
            .IsUnique();

        builder.Entity<ContactMessage>()
            .HasIndex(c => c.CreatedAt);

        builder.Entity<MovieRating>()
            .HasOne(r => r.Movie)
            .WithMany()
            .HasForeignKey(r => r.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MovieRating>()
            .HasIndex(r => new { r.MovieId, r.UserId })
            .IsUnique();

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                prop.SetPrecision(18);
                prop.SetScale(0);
            }
        }
    }
}