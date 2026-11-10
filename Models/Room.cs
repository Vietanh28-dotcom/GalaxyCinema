using System.ComponentModel.DataAnnotations;

namespace GalaxyCinema.Models;

public class Room
{
    public int Id { get; set; }

    [Display(Name = "Chi nhánh")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn chi nhánh")]
    public int CinemaId { get; set; }

    [Required]
    public Cinema Cinema { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập tên phòng")]
    [Display(Name = "Tên phòng")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Loại phòng")]
    public RoomType RoomType { get; set; } = RoomType.Standard2D;

    [Range(1, 500, ErrorMessage = "Sức chứa không hợp lệ")]
    [Display(Name = "Sức chứa")]
    public int Capacity { get; set; }

    public List<Seat> Seats { get; set; } = new();
    public List<Showtime> Showtimes { get; set; } = new();
}