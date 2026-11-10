using GalaxyCinema.Data;
using GalaxyCinema.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var featured = await _db.Movies.Where(m => m.IsFeatured && m.Status == MovieStatus.NowShowing).ToListAsync();
        var nowShowing = await _db.Movies.Where(m => m.Status == MovieStatus.NowShowing).OrderByDescending(m => m.Rating).ToListAsync();
        var comingSoon = await _db.Movies.Where(m => m.Status == MovieStatus.ComingSoon).OrderBy(m => m.ReleaseDate).ToListAsync();

        return View(new HomeViewModel
        {
            FeaturedMovies = featured.Count > 0 ? featured : nowShowing.Take(3).ToList(),
            NowShowing = nowShowing,
            ComingSoon = comingSoon
        });
    }

    public IActionResult Error() => View();
}