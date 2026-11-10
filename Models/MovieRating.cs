using System.ComponentModel.DataAnnotations;
using GalaxyCinema.Services;

namespace GalaxyCinema.Models;

public class MovieRating
{
    public int Id { get; set; }

    public int MovieId { get; set; }

    public string UserId { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Điểm từ 1 đến 5 sao")]
    public int Stars { get; set; }

    public DateTime CreatedAt { get; set; } = VnTime.Now;

    public Movie? Movie { get; set; }
}