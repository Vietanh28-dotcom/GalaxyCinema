using GalaxyCinema.Services;

namespace GalaxyCinema.Models;

public class SeatLock
{
    public int Id { get; set; }

    public int ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;

    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public string SessionId { get; set; } = string.Empty;

    public DateTime LockedAt { get; set; } = VnTime.Now;

    public DateTime ExpiresAt { get; set; }
}