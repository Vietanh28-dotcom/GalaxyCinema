using GalaxyCinema.Data;
using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ShowtimeAutoScheduler _autoScheduler;
    private readonly IWebHostEnvironment _env;

    public AdminController(ApplicationDbContext db, ShowtimeAutoScheduler autoScheduler, IWebHostEnvironment env)
    {
        _db = db;
        _autoScheduler = autoScheduler;
        _env = env;
    }

    private async Task PopulateShowtimeFormAsync()
    {
        ViewBag.Movies = await _db.Movies.OrderBy(m => m.Title).ToListAsync();
        ViewBag.Rooms = await _db.Rooms.Include(r => r.Cinema).OrderBy(r => r.Cinema.Name).ThenBy(r => r.Name).Select(r => new { r.Id, Label = r.Cinema.Name + " - " + r.Name + " (" + (r.RoomType == RoomType.IMAX ? "IMAX" : r.RoomType == RoomType.Standard3D ? "3D" : "2D") + ")" }).ToListAsync();
    }

    private async Task<bool> ShowtimeOverlapsAsync(int roomId, DateTime start, DateTime end, int exceptId = 0)
    {
        var query = _db.Showtimes.Where(s => s.RoomId == roomId && s.StartTime < end && s.EndTime > start);
        if (exceptId > 0) query = query.Where(s => s.Id != exceptId);
        return await query.AnyAsync();
    }

    // ---------- Dashboard ----------

    public async Task<IActionResult> Index()
    {
        var today = VnTime.Today;
        var bookings = await _db.Bookings
            .Include(b => b.Showtime)
            .ThenInclude(s => s.Movie)
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToListAsync();

        ViewBag.TotalBookings = bookings.Count;
        ViewBag.TotalRevenue = bookings.Sum(b => b.TotalAmount);
        ViewBag.TodayRevenue = bookings.Where(b => b.Showtime != null && b.Showtime.StartTime.Date == today).Sum(b => b.TotalAmount);
        ViewBag.MovieCount = await _db.Movies.CountAsync();
        ViewBag.CinemaCount = await _db.Cinemas.CountAsync();
        ViewBag.TotalSeatsSold = await _db.BookingSeats.CountAsync(bs => bs.Booking.Status != BookingStatus.Cancelled);

        // Doanh thu theo ngày chiếu (7 ngày gần nhất)
        var revenueByDay = bookings
            .Where(b => b.Showtime != null)
            .GroupBy(b => b.Showtime.StartTime.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(b => b.TotalAmount) })
            .OrderBy(x => x.Date)
            .ToList();
        ViewBag.RevenueByDay = revenueByDay;

        // Doanh thu theo phim
        var revenueByMovie = bookings
            .GroupBy(b => b.Showtime.MovieId)
            .Select(g => new
            {
                MovieId = g.Key,
                MovieTitle = g.First().Showtime.Movie.Title,
                Total = g.Sum(b => b.TotalAmount),
                Count = g.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToList();
        ViewBag.RevenueByMovie = revenueByMovie;

        return View();
    }

    // ---------- Movies ----------

    public async Task<IActionResult> Movies()
    {
        var movies = await _db.Movies.OrderByDescending(m => m.ReleaseDate).ToListAsync();
        return View(movies);
    }

    [HttpGet]
    public IActionResult CreateMovie() => View(new Movie());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMovie(Movie movie)
    {
        if (!ModelState.IsValid) return View(movie);
        if (await _db.Movies.AnyAsync(m => m.Title == movie.Title.Trim()))
            ModelState.AddModelError("Title", "Tên phim này đã tồn tại.");
        if (!ModelState.IsValid) return View(movie);
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Movies));
    }

    [HttpGet]
    public async Task<IActionResult> EditMovie(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie == null) return NotFound();
        return View(movie);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMovie(Movie movie)
    {
        if (!ModelState.IsValid) return View(movie);
        if (await _db.Movies.AnyAsync(m => m.Title == movie.Title.Trim() && m.Id != movie.Id))
            ModelState.AddModelError("Title", "Tên phim này đã tồn tại.");
        if (!ModelState.IsValid) return View(movie);
        _db.Entry(movie).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Movies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie != null)
        {
            if (await _db.Showtimes.AnyAsync(s => s.MovieId == id))
            {
                TempData["AdminError"] = "Không thể xóa phim này vì còn suất chiếu/đơn đặt vé liên quan.";
                return RedirectToAction(nameof(Movies));
            }
            _db.Movies.Remove(movie);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Movies));
    }

    // ---------- Showtimes ----------

    public async Task<IActionResult> Showtimes()
    {
        var list = await _db.Showtimes
            .Include(st => st.Movie)
            .Include(st => st.Room).ThenInclude(r => r.Cinema)
            .OrderByDescending(st => st.StartTime)
            .ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoGenerateShowtimes()
    {
        var created = await _autoScheduler.GenerateAsync(HttpContext.RequestAborted);
        TempData["Success"] = created > 0
            ? $"Đã tự động tạo {created} suất chiếu mới theo ngày giờ hiện tại của Việt Nam."
            : "Lịch chiếu đã đầy đủ, không có suất chiếu mới cần tạo.";
        return RedirectToAction(nameof(Showtimes));
    }

    [HttpGet]
    public async Task<IActionResult> CreateShowtime()
    {
        await PopulateShowtimeFormAsync();
        return View(new Showtime { StartTime = VnTime.Today.AddDays(1).AddHours(18), IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateShowtime(Showtime showtime)
    {
        ModelState.Remove("Movie");
        ModelState.Remove("Room");
        if (!ModelState.IsValid)
        {
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }

        var movie = await _db.Movies.FindAsync(showtime.MovieId);
        if (movie == null)
            ModelState.AddModelError("MovieId", "Vui lòng chọn phim.");
        if (movie != null && movie.Status == MovieStatus.ComingSoon)
            ModelState.AddModelError("MovieId", "Không thể tạo suất chiếu cho phim chưa công chiếu.");
        if (movie != null && showtime.StartTime.Date < movie.ReleaseDate.Date)
            ModelState.AddModelError("StartTime", $"Suất chiếu phải bắt đầu từ ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");
        if (await _db.Rooms.FindAsync(showtime.RoomId) == null)
            ModelState.AddModelError("RoomId", "Vui lòng chọn phòng.");

        showtime.EndTime = showtime.StartTime.AddMinutes(movie?.DurationMinutes ?? 120);

        if (showtime.StartTime < VnTime.Now.AddMinutes(-10))
            ModelState.AddModelError("StartTime", "Không thể tạo suất chiếu trong quá khứ.");

        if (showtime.EndTime <= showtime.StartTime)
            ModelState.AddModelError("StartTime", "Giờ kết thúc phải sau giờ bắt đầu.");

        if (ModelState.IsValid && await ShowtimeOverlapsAsync(showtime.RoomId, showtime.StartTime, showtime.EndTime))
            ModelState.AddModelError("", $"Phòng này đã có suất chiếu trùng khung giờ {showtime.StartTime:HH:mm} - {showtime.EndTime:HH:mm}.");

        if (!ModelState.IsValid)
        {
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }
        _db.Showtimes.Add(showtime);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Showtimes));
    }

    [HttpGet]
    public async Task<IActionResult> EditShowtime(int id)
    {
        var showtime = await _db.Showtimes.FindAsync(id);
        if (showtime == null) return NotFound();
        await PopulateShowtimeFormAsync();
        return View(showtime);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditShowtime(Showtime showtime)
    {
        ModelState.Remove("Movie");
        ModelState.Remove("Room");
        if (!ModelState.IsValid)
        {
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }

        var original = await _db.Showtimes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == showtime.Id);
        if (original == null) return NotFound();
        var hasBookings = await _db.Bookings.AnyAsync(b => b.ShowtimeId == showtime.Id);
        if (hasBookings && (original.MovieId != showtime.MovieId || original.RoomId != showtime.RoomId
                || original.StartTime != showtime.StartTime || original.BasePrice != showtime.BasePrice))
        {
            ModelState.AddModelError("", "Không thể sửa phim, phòng, giờ chiếu hoặc giá vì suất chiếu này đã có vé đặt.");
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }

        var movie = await _db.Movies.FindAsync(showtime.MovieId);
        if (movie == null)
        {
            ModelState.AddModelError("MovieId", "Vui lòng chọn phim.");
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }
        if (movie.Status == MovieStatus.ComingSoon)
        {
            ModelState.AddModelError("MovieId", "Không thể tạo suất chiếu cho phim chưa công chiếu.");
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }
        if (showtime.StartTime.Date < movie.ReleaseDate.Date)
        {
            ModelState.AddModelError("StartTime", $"Suất chiếu phải bắt đầu từ ngày khởi chiếu {movie.ReleaseDate:dd/MM/yyyy}.");
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }
        if (await _db.Rooms.FindAsync(showtime.RoomId) == null)
        {
            ModelState.AddModelError("RoomId", "Vui lòng chọn phòng.");
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }

        showtime.EndTime = showtime.StartTime.AddMinutes(movie.DurationMinutes);

        if (showtime.StartTime < VnTime.Now.AddMinutes(-10))
            ModelState.AddModelError("StartTime", "Không thể sửa suất chiếu sang quá khứ.");

        if (showtime.EndTime <= showtime.StartTime)
            ModelState.AddModelError("StartTime", "Giờ kết thúc phải sau giờ bắt đầu.");

        if (ModelState.IsValid && await ShowtimeOverlapsAsync(showtime.RoomId, showtime.StartTime, showtime.EndTime, showtime.Id))
            ModelState.AddModelError("", $"Phòng này đã có suất chiếu trùng khung giờ {showtime.StartTime:HH:mm} - {showtime.EndTime:HH:mm}.");

        if (!ModelState.IsValid)
        {
            await PopulateShowtimeFormAsync();
            return View(showtime);
        }
        _db.Entry(showtime).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Showtimes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShowtime(int id)
    {
        var showtime = await _db.Showtimes.FindAsync(id);
        if (showtime != null)
        {
            if (await _db.Bookings.AnyAsync(b => b.ShowtimeId == id))
            {
                TempData["AdminError"] = "Không thể xóa suất chiếu này vì đã có đơn đặt vé.";
                return RedirectToAction(nameof(Showtimes));
            }
            _db.Showtimes.Remove(showtime);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Showtimes));
    }

    // ---------- Cinemas ----------

    public async Task<IActionResult> Cinemas()
    {
        var list = await _db.Cinemas.Include(c => c.Rooms).OrderBy(c => c.Name).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateCinema() => View(new Cinema());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCinema(Cinema cinema)
    {
        if (!ModelState.IsValid) return View(cinema);
        if (await _db.Cinemas.AnyAsync(c => c.Name == cinema.Name.Trim()))
            ModelState.AddModelError("Name", "Tên rạp này đã tồn tại.");
        if (!ModelState.IsValid) return View(cinema);
        _db.Cinemas.Add(cinema);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Cinemas));
    }

    [HttpGet]
    public async Task<IActionResult> EditCinema(int id)
    {
        var cinema = await _db.Cinemas.FindAsync(id);
        if (cinema == null) return NotFound();
        return View(cinema);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCinema(Cinema cinema)
    {
        if (!ModelState.IsValid) return View(cinema);
        if (await _db.Cinemas.AnyAsync(c => c.Name == cinema.Name.Trim() && c.Id != cinema.Id))
            ModelState.AddModelError("Name", "Tên rạp này đã tồn tại.");
        if (!ModelState.IsValid) return View(cinema);
        _db.Entry(cinema).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Cinemas));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCinema(int id)
    {
        var cinema = await _db.Cinemas.Include(c => c.Rooms).FirstOrDefaultAsync(c => c.Id == id);
        if (cinema != null)
        {
            var roomIds = cinema.Rooms.Select(r => r.Id).ToList();
            if (roomIds.Any())
            {
                var hasShowtimes = await _db.Showtimes.AnyAsync(s => roomIds.Contains(s.RoomId));
                var hasBookings = await _db.BookingSeats.AnyAsync(bs => roomIds.Contains(bs.Seat.RoomId));
                if (hasShowtimes || hasBookings)
                {
                    TempData["AdminError"] = "Không thể xóa rạp này vì còn suất chiếu/đơn đặt vé liên quan.";
                    return RedirectToAction(nameof(Cinemas));
                }
            }
            _db.Cinemas.Remove(cinema);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Cinemas));
    }

    // ---------- Rooms ----------

    public async Task<IActionResult> Rooms()
    {
        var list = await _db.Rooms.Include(r => r.Cinema).Include(r => r.Seats).OrderBy(r => r.Name).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> CreateRoom()
    {
        ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
        return View(new Room());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(Room room)
    {
        ModelState.Remove("Cinema");
        ModelState.Remove("Capacity");
        if (!ModelState.IsValid)
        {
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        if (await _db.Rooms.AnyAsync(r => r.CinemaId == room.CinemaId && r.Name == room.Name.Trim()))
            ModelState.AddModelError("Name", "Phòng này đã tồn tại trong chi nhánh đã chọn.");
        if (!ModelState.IsValid)
        {
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        room.Capacity = 0;
        try
        {
            _db.Rooms.Add(room);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể lưu phòng. Vui lòng kiểm tra lại thông tin và Chi nhánh đã chọn.");
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        TempData["Success"] = $"Đã tạo phòng '{room.Name}'.";
        return RedirectToAction(nameof(Rooms));
    }

    [HttpGet]
    public async Task<IActionResult> EditRoom(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();
        ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
        return View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(Room room)
    {
        ModelState.Remove("Cinema");
        ModelState.Remove("Capacity");
        if (!ModelState.IsValid)
        {
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        var original = await _db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == room.Id);
        if (original == null) return NotFound();
        if (original.CinemaId != room.CinemaId && await _db.Showtimes.AnyAsync(s => s.RoomId == room.Id))
            ModelState.AddModelError("CinemaId", "Không thể chuyển phòng sang chi nhánh khác vì phòng này đang có suất chiếu.");
        if (await _db.Rooms.AnyAsync(r => r.CinemaId == room.CinemaId && r.Name == room.Name.Trim() && r.Id != room.Id))
            ModelState.AddModelError("Name", "Phòng này đã tồn tại trong chi nhánh đã chọn.");
        if (!ModelState.IsValid)
        {
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        room.Capacity = await _db.Seats.CountAsync(s => s.RoomId == room.Id);
        try
        {
            _db.Entry(room).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể lưu thay đổi. Vui lòng kiểm tra lại thông tin và Chi nhánh đã chọn.");
            ViewBag.Cinemas = await _db.Cinemas.OrderBy(c => c.Name).ToListAsync();
            return View(room);
        }
        TempData["Success"] = $"Đã cập nhật phòng '{room.Name}'.";
        return RedirectToAction(nameof(Rooms));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room != null)
        {
            if (await _db.Showtimes.AnyAsync(s => s.RoomId == id))
            {
                TempData["AdminError"] = "Không thể xóa phòng này vì còn suất chiếu liên quan.";
                return RedirectToAction(nameof(Rooms));
            }
            _db.Rooms.Remove(room);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Rooms));
    }

    // ---------- Seats ----------

    public async Task<IActionResult> Seats(int? roomId)
    {
        var rooms = await _db.Rooms.Include(r => r.Cinema).OrderBy(r => r.Name).ToListAsync();
        var selected = roomId ?? rooms.FirstOrDefault()?.Id ?? 0;
        var seats = selected > 0
            ? await _db.Seats.Where(s => s.RoomId == selected).OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ToListAsync()
            : new List<Seat>();
        ViewBag.Rooms = rooms;
        ViewBag.SelectedRoomId = selected;
        return View(seats);
    }

    [HttpGet]
    public async Task<IActionResult> CreateSeat(int roomId)
    {
        var room = await _db.Rooms.FindAsync(roomId);
        if (room == null) return NotFound();
        ViewBag.Room = room;
        return View(new Seat { RoomId = roomId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSeat(Seat seat)
    {
        ModelState.Remove("Room");
        if (!ModelState.IsValid)
        {
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }
        if (await _db.Seats.AnyAsync(s => s.RoomId == seat.RoomId && s.Code == seat.Code))
            ModelState.AddModelError("Code", $"Mã ghế '{seat.Code}' đã tồn tại trong phòng này.");
        if (await _db.Seats.AnyAsync(s => s.RoomId == seat.RoomId && s.RowNumber == seat.RowNumber && s.ColumnNumber == seat.ColumnNumber))
            ModelState.AddModelError("", $"Đã có ghế tại hàng {seat.RowNumber}, cột {seat.ColumnNumber}.");
        if (!ModelState.IsValid)
        {
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }

        var room = await _db.Rooms.FindAsync(seat.RoomId);
        if (room == null) return NotFound();
        seat.PriceMultiplier = seat.SeatType switch
        {
            SeatType.Vip => 1.5m,
            SeatType.Couple => 2m,
            _ => 1m
        };
        try
        {
            _db.Seats.Add(seat);
            await _db.SaveChangesAsync();
            room.Capacity = await _db.Seats.CountAsync(s => s.RoomId == room.Id);
            _db.Entry(room).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể lưu ghế. Có thể mã ghế hoặc vị trí đã bị trùng, vui lòng kiểm tra lại.");
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }
        return RedirectToAction(nameof(Seats), new { roomId = seat.RoomId });
    }

    [HttpGet]
    public async Task<IActionResult> EditSeat(int id)
    {
        var seat = await _db.Seats.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == id);
        if (seat == null) return NotFound();
        ViewBag.Room = seat.Room;
        return View(seat);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSeat(Seat seat)
    {
        ModelState.Remove("Room");
        if (!ModelState.IsValid)
        {
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }

        var original = await _db.Seats.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seat.Id);
        if (original == null) return NotFound();
        if (await _db.Rooms.FindAsync(seat.RoomId) == null)
            ModelState.AddModelError("RoomId", "Phòng không tồn tại.");

        var hasBookings = await _db.BookingSeats.AnyAsync(bs => bs.SeatId == seat.Id);
        if (hasBookings && original.RoomId != seat.RoomId)
            ModelState.AddModelError("", "Không thể chuyển ghế sang phòng khác vì ghế này đã có vé đặt.");
        if (hasBookings && original.SeatType != seat.SeatType)
            ModelState.AddModelError("SeatType", "Không thể đổi loại ghế vì ghế này đã có vé đặt.");

        if (await _db.Seats.AnyAsync(s => s.RoomId == seat.RoomId && s.Code == seat.Code && s.Id != seat.Id))
            ModelState.AddModelError("Code", $"Mã ghế '{seat.Code}' đã tồn tại trong phòng này.");
        if (await _db.Seats.AnyAsync(s => s.RoomId == seat.RoomId && s.RowNumber == seat.RowNumber && s.ColumnNumber == seat.ColumnNumber && s.Id != seat.Id))
            ModelState.AddModelError("", $"Đã có ghế tại hàng {seat.RowNumber}, cột {seat.ColumnNumber}.");
        if (!ModelState.IsValid)
        {
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }
        seat.PriceMultiplier = seat.SeatType switch
        {
            SeatType.Vip => 1.5m,
            SeatType.Couple => 2m,
            _ => 1m
        };
        try
        {
            _db.Entry(seat).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Không thể lưu ghế. Có thể mã ghế hoặc vị trí đã bị trùng, vui lòng kiểm tra lại.");
            ViewBag.Room = await _db.Rooms.FindAsync(seat.RoomId);
            return View(seat);
        }
        return RedirectToAction(nameof(Seats), new { roomId = seat.RoomId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSeat(int id)
    {
        var seat = await _db.Seats.FindAsync(id);
        if (seat != null)
        {
            if (await _db.BookingSeats.AnyAsync(bs => bs.SeatId == id))
            {
                TempData["AdminError"] = "Không thể xóa ghế này vì đã có vé đặt.";
                return RedirectToAction(nameof(Seats), new { roomId = seat.RoomId });
            }
            var roomId = seat.RoomId;
            _db.Seats.Remove(seat);
            await _db.SaveChangesAsync();
            var room = await _db.Rooms.FindAsync(roomId);
            if (room != null)
            {
                room.Capacity = await _db.Seats.CountAsync(s => s.RoomId == roomId);
                _db.Entry(room).State = EntityState.Modified;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Seats), new { roomId });
        }
        return RedirectToAction(nameof(Seats));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSeats(int roomId, int rows, int cols)
    {
        rows = Math.Clamp(rows, 1, 20);
        cols = Math.Clamp(cols, 1, 30);
        var room = await _db.Rooms.FindAsync(roomId);
        if (room == null) return NotFound();

        if (await _db.Seats.AnyAsync(s => s.RoomId == roomId))
        {
            TempData["AdminError"] = "Phòng đã có ghế, không thể sinh tự động.";
            return RedirectToAction(nameof(Seats), new { roomId });
        }

        for (int row = 1; row <= rows; row++)
        {
            var seatType = SeatType.Normal;
            decimal multiplier = 1m;
            if (row > rows - 1) { seatType = SeatType.Couple; multiplier = 2m; }
            else if (row > rows - 2) { seatType = SeatType.Vip; multiplier = 1.5m; }

            for (int col = 1; col <= cols; col++)
            {
                _db.Seats.Add(new Seat
                {
                    RoomId = roomId,
                    Code = $"{Convert.ToChar('A' + row - 1)}{col}",
                    RowNumber = row,
                    ColumnNumber = col,
                    SeatType = seatType,
                    PriceMultiplier = multiplier
                });
            }
        }
        room.Capacity = rows * cols;
        _db.Entry(room).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Seats), new { roomId });
    }

    // ---------- Combos ----------

    public async Task<IActionResult> Combos()
    {
        var list = await _db.Combos.OrderBy(c => c.Name).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateCombo() => View(new Combo());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCombo(Combo combo, IFormFile? imageFile)
    {
        if (!ModelState.IsValid) return View(combo);

        combo.Name = combo.Name.Trim();
        if (await _db.Combos.AnyAsync(c => c.Name.ToLower() == combo.Name.ToLower()))
            ModelState.AddModelError("Name", "Tên combo này đã tồn tại.");

        if (!ModelState.IsValid) return View(combo);

        try
        {
            var uploaded = await SaveComboImageAsync(imageFile);
            if (uploaded != null) combo.ImageUrl = uploaded;

            _db.Combos.Add(combo);
            await _db.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ImageUrl", ex.Message);
            return View(combo);
        }

        TempData["Success"] = $"Đã tạo combo '{combo.Name}'.";
        return RedirectToAction(nameof(Combos));
    }

    [HttpGet]
    public async Task<IActionResult> EditCombo(int id)
    {
        var combo = await _db.Combos.FindAsync(id);
        if (combo == null) return NotFound();
        return View(combo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCombo(Combo combo, IFormFile? imageFile)
    {
        if (!ModelState.IsValid) return View(combo);

        combo.Name = combo.Name.Trim();
        if (await _db.Combos.AnyAsync(c => c.Id != combo.Id && c.Name.ToLower() == combo.Name.ToLower()))
            ModelState.AddModelError("Name", "Tên combo này đã tồn tại.");

        if (!ModelState.IsValid) return View(combo);

        try
        {
            var uploaded = await SaveComboImageAsync(imageFile);
            if (uploaded != null) combo.ImageUrl = uploaded;

            _db.Entry(combo).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ImageUrl", ex.Message);
            return View(combo);
        }

        TempData["Success"] = $"Đã cập nhật combo '{combo.Name}'.";
        return RedirectToAction(nameof(Combos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCombo(int id)
    {
        var combo = await _db.Combos.FindAsync(id);
        if (combo == null) return NotFound();

        combo.IsActive = !combo.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = combo.IsActive ? $"Đã bật combo '{combo.Name}'." : $"Đã ẩn combo '{combo.Name}'.";
        return RedirectToAction(nameof(Combos));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCombo(int id)
    {
        var combo = await _db.Combos.FindAsync(id);
        if (combo != null)
        {
            _db.Combos.Remove(combo);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa combo '{combo.Name}'.";
        }
        return RedirectToAction(nameof(Combos));
    }

    private static readonly string[] ComboImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
    private const long ComboImageMaxBytes = 5 * 1024 * 1024;

    private async Task<string?> SaveComboImageAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0) return null;

        if (imageFile.Length > ComboImageMaxBytes)
            throw new InvalidOperationException("Ảnh vượt quá dung lượng cho phép (tối đa 5MB).");

        var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        if (!ComboImageExtensions.Contains(ext))
            throw new InvalidOperationException("Định dạng ảnh không hợp lệ. Chỉ nhận .jpg, .jpeg, .png, .gif, .webp, .svg.");

        var folder = Path.Combine(_env.WebRootPath, "uploads", "combos");
        Directory.CreateDirectory(folder);

        var fileName = Guid.NewGuid().ToString("N") + ext;
        var fullPath = Path.Combine(folder, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await imageFile.CopyToAsync(stream);
        }

        return "/uploads/combos/" + fileName;
    }

    // ---------- Bookings ----------

    public async Task<IActionResult> Bookings(BookingStatus? status, DateTime? from, DateTime? to)
    {
        var query = _db.Bookings
            .Include(b => b.Showtime).ThenInclude(st => st.Movie)
            .Include(b => b.Showtime).ThenInclude(st => st.Room).ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .AsQueryable();

        if (status.HasValue) query = query.Where(b => b.Status == status.Value);
        if (from.HasValue) query = query.Where(b => b.CreatedAt.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(b => b.CreatedAt.Date <= to.Value.Date);

        var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        ViewBag.Status = status;
        ViewBag.From = from;
        ViewBag.To = to;
        return View(list);
    }

    public async Task<IActionResult> BookingDetails(string code)
    {
        var booking = await _db.Bookings
            .Include(b => b.Showtime).ThenInclude(st => st.Movie)
            .Include(b => b.Showtime).ThenInclude(st => st.Room).ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking == null) return NotFound();
        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(string code)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking != null && booking.Status == BookingStatus.Pending)
        {
            booking.Status = BookingStatus.Paid;
            _db.Entry(booking).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(BookingDetails), new { code });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(string code)
    {
        var booking = await _db.Bookings
            .Include(b => b.Showtime)
            .FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking != null)
        {
            if (booking.Showtime.StartTime <= VnTime.Now)
            {
                TempData["AdminError"] = "Không thể hủy đơn vé vì suất chiếu đã bắt đầu.";
                return RedirectToAction(nameof(BookingDetails), new { code });
            }
            booking.Status = BookingStatus.Cancelled;
            _db.Entry(booking).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Bookings));
    }
}