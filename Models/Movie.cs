using System.ComponentModel.DataAnnotations;
using GalaxyCinema.Services;

namespace GalaxyCinema.Models;

public class Movie
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên phim")]
    [Display(Name = "Tên phim")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Thể loại")]
    public string? Genre { get; set; }

    [Range(1, 600, ErrorMessage = "Thời lượng không hợp lệ")]
    [Display(Name = "Thời lượng (phút)")]
    public int DurationMinutes { get; set; } = 120;

    [Display(Name = "Đạo diễn")]
    public string? Director { get; set; }

    [Display(Name = "Diễn viên")]
    public string? Cast { get; set; }

    [Display(Name = "Ảnh poster")]
    public string? PosterUrl { get; set; }

    [Display(Name = "Link trailer")]
    public string? TrailerUrl { get; set; }

    [Range(0, 10, ErrorMessage = "Rating từ 0 đến 10")]
    [Display(Name = "Điểm đánh giá")]
    public double Rating { get; set; }

    [Display(Name = "Trạng thái")]
    public MovieStatus Status { get; set; } = MovieStatus.NowShowing;

    [DataType(DataType.Date)]
    [Display(Name = "Ngày khởi chiếu")]
    public DateTime ReleaseDate { get; set; } = VnTime.Today;

    [Display(Name = "Phim nổi bật")]
    public bool IsFeatured { get; set; }

    public List<Showtime> Showtimes { get; set; } = new();
}