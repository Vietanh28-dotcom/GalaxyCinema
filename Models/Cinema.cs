using System.ComponentModel.DataAnnotations;

namespace GalaxyCinema.Models;

public class Cinema
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên chi nhánh")]
    [Display(Name = "Tên chi nhánh")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ")]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [Display(Name = "Mã nhúng bản đồ (tùy chọn)")]
    public string? MapEmbedUrl { get; set; }

    public List<Room> Rooms { get; set; } = new();
}