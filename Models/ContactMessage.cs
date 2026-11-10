using System.ComponentModel.DataAnnotations;
using GalaxyCinema.Services;

namespace GalaxyCinema.Models;

public class ContactMessage
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung")]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = VnTime.Now;
}