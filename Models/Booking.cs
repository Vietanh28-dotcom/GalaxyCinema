using System.ComponentModel.DataAnnotations;
using GalaxyCinema.Services;

namespace GalaxyCinema.Models;

public class Booking
{
    public int Id { get; set; }

    [Display(Name = "Mã vé")]
    public string BookingCode { get; set; } = string.Empty;

    public int ShowtimeId { get; set; }

    [Required]
    public Showtime Showtime { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [Display(Name = "Họ tên khách hàng")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Display(Name = "Ghi chú")]
    public string? CustomerNote { get; set; }

    [Display(Name = "Tiền ghế")]
    public decimal SeatAmount { get; set; }

    [Display(Name = "Tiền combo")]
    public decimal ComboAmount { get; set; }

    [Display(Name = "Tổng tiền")]
    public decimal TotalAmount { get; set; }

    [Display(Name = "Trạng thái")]
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [Display(Name = "Phương thức thanh toán")]
    public PaymentMethod? PaymentMethod { get; set; }

    [Display(Name = "Thời gian đặt")]
    public DateTime CreatedAt { get; set; } = VnTime.Now;

    [Display(Name = "Combo đã chọn")]
    public string? ComboSummary { get; set; }

    [Display(Name = "QR code")]
    public string? QrCodeBase64 { get; set; }

    public List<BookingSeat> BookingSeats { get; set; } = new();
}