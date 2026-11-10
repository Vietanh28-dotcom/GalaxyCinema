using GalaxyCinema.Data;
using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Controllers;

public class CinemaController : Controller
{
    private readonly ApplicationDbContext _db;

    public CinemaController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var cinemas = await _db.Cinemas.Include(c => c.Rooms).ToListAsync();
        return View(cinemas);
    }

    public async Task<IActionResult> Details(int id)
    {
        var cinema = await _db.Cinemas
            .Include(c => c.Rooms)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cinema == null) return NotFound();

        // Lịch chiếu sắp tới, tự động cập nhật theo ngày giờ Việt Nam.
        ViewBag.UpcomingShowtimes = await _db.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .Where(s => s.Room.CinemaId == id && s.IsActive && s.StartTime > VnTime.Now)
            .OrderBy(s => s.StartTime)
            .Take(80)
            .ToListAsync();

        return View(cinema);
    }
}