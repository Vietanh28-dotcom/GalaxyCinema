namespace GalaxyCinema.Models;

public class BookingSeat
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public decimal Price { get; set; }
}