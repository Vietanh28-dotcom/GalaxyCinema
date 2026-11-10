using GalaxyCinema.Data;
using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Controllers;

public class MovieController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public MovieController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? genre, MovieStatus? status)
    {
        var genreStrings = await _db.Movies
            .Where(m => m.Genre != null)
            .Select(m => m.Genre!)
            .ToListAsync();

var genres = genreStrings
            .SelectMany(g => g.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        var query = _db.Movies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        var movies = await query
            .OrderByDescending(m => m.Status == MovieStatus.NowShowing)
            .ThenByDescending(m => m.Rating)
            .ToListAsync();

        return View(new MovieIndexViewModel
        {
            Movies = movies,
            Genres = genres,
            SelectedGenre = genre,
            SelectedStatus = status
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var movie = await _db.Movies
            .Include(m => m.Showtimes)
                .ThenInclude(st => st.Room)
                .ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null) return NotFound();

        movie.Showtimes = movie.Showtimes
            .Where(st => st.IsActive && st.StartTime > VnTime.Now)
            .OrderBy(st => st.StartTime)
            .ToList();

        var avgRating = await _db.MovieRatings
            .Where(r => r.MovieId == id)
            .AverageAsync(r => (double?)r.Stars);
        ViewBag.AverageRating = avgRating?.ToString("0.0");
        ViewBag.RatingCount = await _db.MovieRatings.CountAsync(r => r.MovieId == id);

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var myRating = await _db.MovieRatings
                    .FirstOrDefaultAsync(r => r.MovieId == id && r.UserId == user.Id);
                ViewBag.MyStars = myRating?.Stars ?? 0;
            }
        }

        return View(movie);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rate(int movieId, int stars)
    {
        if (stars < 1 || stars > 5) return BadRequest();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var existing = await _db.MovieRatings
            .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == user.Id);
        if (existing != null)
        {
            existing.Stars = stars;
        }
        else
        {
            _db.MovieRatings.Add(new MovieRating
            {
                MovieId = movieId,
                UserId = user.Id,
                Stars = stars,
                CreatedAt = VnTime.Now
            });
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = movieId });
    }
}