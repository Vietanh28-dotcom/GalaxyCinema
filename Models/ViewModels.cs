namespace GalaxyCinema.Models;

public class HomeViewModel
{
    public List<Movie> FeaturedMovies { get; set; } = new();
    public List<Movie> NowShowing { get; set; } = new();
    public List<Movie> ComingSoon { get; set; } = new();
}

public class MovieIndexViewModel
{
    public List<Movie> Movies { get; set; } = new();
    public List<string> Genres { get; set; } = new();
    public string? SelectedGenre { get; set; }
    public MovieStatus? SelectedStatus { get; set; }
}

public class SelectShowtimeViewModel
{
    public Movie Movie { get; set; } = null!;
    public List<Cinema> Cinemas { get; set; } = new();
    public int? SelectedCinemaId { get; set; }
    public DateTime? SelectedDate { get; set; }
    public List<DateTime> AvailableDates { get; set; } = new();
    public List<Showtime> Showtimes { get; set; } = new();
    public int? Format { get; set; }
    public List<int> AvailableFormats { get; set; } = new();
}

public class SeatMapViewModel
{
    public Showtime Showtime { get; set; } = null!;
    public List<IGrouping<int, Seat>> Rows { get; set; } = new List<IGrouping<int, Seat>>();
    public HashSet<int> OccupiedSeatIds { get; set; } = new();
    public HashSet<int> MyLockedSeatIds { get; set; } = new();
    public HashSet<int> OtherLockedSeatIds { get; set; } = new();
    public int MaxColumns { get; set; }
}

public class SeatStatusDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = "available"; // available | booked | locked | mine
}

public class SelectComboViewModel
{
    public Showtime Showtime { get; set; } = null!;
    public List<Combo> Combos { get; set; } = new();
    public List<Seat> SelectedSeats { get; set; } = new();
    public decimal SeatTotal { get; set; }
    public List<int> SeatIds { get; set; } = new();
}

public class CustomerInfoViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Showtime Showtime { get; set; } = null!;
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public List<Seat> SelectedSeats { get; set; } = new();
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public List<ComboLine> ComboLines { get; set; } = new();
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public List<int> SeatIds { get; set; } = new();
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public decimal SeatTotal { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public decimal ComboTotal { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public decimal TotalAmount { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Họ tên")]
    public string CustomerName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [System.ComponentModel.DataAnnotations.Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Số điện thoại")]
    public string CustomerPhone { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập email")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Ghi chú")]
    public string? CustomerNote { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.AtCounter;
}

public class ComboLine
{
    public int ComboId { get; set; }
    public string ComboName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => Price * Quantity;
}