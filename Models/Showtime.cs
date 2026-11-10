using System.ComponentModel.DataAnnotations;

namespace GalaxyCinema.Models;

public class Showtime
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public int RoomId { get; set; }

    public Room Room { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng chọn ngày giờ chiếu")]
    [Display(Name = "Ngày giờ chiếu")]
    public DateTime StartTime { get; set; }

    [Display(Name = "Kết thúc")]
    public DateTime EndTime { get; set; }

    [Range(10000, 10000000, ErrorMessage = "Giá vé không hợp lệ")]
    [Display(Name = "Giá vé (VNĐ)")]
    public decimal BasePrice { get; set; }

    [Display(Name = "Còn chiếu")]
    public bool IsActive { get; set; } = true;

    public List<Booking> Bookings { get; set; } = new();
}