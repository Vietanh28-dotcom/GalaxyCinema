using System.ComponentModel.DataAnnotations;

namespace GalaxyCinema.Models;

public class Combo
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên combo")]
    [MaxLength(100, ErrorMessage = "Tên combo tối đa 100 ký tự")]
    [Display(Name = "Tên combo")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Range(1, 10000000, ErrorMessage = "Giá phải lớn hơn 0 và không quá 10.000.000 ₫")]
    [Display(Name = "Giá (VNĐ)")]
    public decimal Price { get; set; }

    [MaxLength(500)]
    [Display(Name = "Ảnh")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Kích hoạt")]
    public bool IsActive { get; set; } = true;
}
