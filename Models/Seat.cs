using System.ComponentModel.DataAnnotations;

namespace GalaxyCinema.Models;

public class Seat
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    [Required]
    public Room Room { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập mã ghế")]
    [Display(Name = "Mã ghế")]
    public string Code { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Hàng phải lớn hơn 0")]
    [Display(Name = "Hàng")]
    public int RowNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Cột phải lớn hơn 0")]
    [Display(Name = "Cột")]
    public int ColumnNumber { get; set; }

    [Display(Name = "Loại ghế")]
    public SeatType SeatType { get; set; } = SeatType.Normal;

    [Range(1, 5, ErrorMessage = "Hệ số giá không hợp lệ")]
    [Display(Name = "Hệ số giá")]
    public decimal PriceMultiplier { get; set; } = 1m;

    public List<BookingSeat> BookingSeats { get; set; } = new();
    public List<SeatLock> Locks { get; set; } = new();
}