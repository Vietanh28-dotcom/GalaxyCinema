using GalaxyCinema.Data;
using GalaxyCinema.Models;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Services;

/// <summary>
/// Sinh lịch chiếu tự động: các suất chiếu cách nhau đúng 1 tiếng, luân phiên chia đều
/// cho các rạp và tự chọn phòng còn trống để không bị trùng giờ.
/// </summary>
public static class ShowtimePlanner
{
    private const int OpeningHour = 9;   // suất đầu tiên lúc 09:00
    private const int ClosingHour = 22;  // suất cuối bắt đầu lúc 22:00
    private const int DaysAhead = 2;     // hôm nay + 2 ngày tới
    private static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(30);

    public static async Task<int> GenerateAsync(ApplicationDbContext db, DateTime now, CancellationToken ct = default)
    {
        var firstDay = now.Date;
        var lastDay = firstDay.AddDays(DaysAhead);

        var movies = await db.Movies
            .Where(m => m.Status == MovieStatus.NowShowing)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        var cinemas = await db.Cinemas
            .Include(c => c.Rooms)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        if (movies.Count == 0 || cinemas.Count == 0) return 0;

        // Nạp sẵn suất chiếu trong khoảng sẽ tạo để kiểm tra trùng/overlap.
        var existing = await db.Showtimes
            .Where(s => s.StartTime >= firstDay && s.StartTime < lastDay.AddDays(1))
            .ToListAsync(ct);

        var created = 0;
        var cinemaCursor = 0;
        var movieCursor = 0;

        for (var day = 0; day <= DaysAhead; day++)
        {
            var date = firstDay.AddDays(day);

            // Mỗi khung giờ cách nhau đúng 1 tiếng.
            for (var hour = OpeningHour; hour <= ClosingHour; hour++)
            {
                var start = date.AddHours(hour);
                if (start < now + LeadTime) continue;

                var placed = false;

                // Luân phiên rạp: mỗi suất được chia lần lượt cho các rạp khác nhau.
                for (var c = 0; c < cinemas.Count && !placed; c++)
                {
                    var cinemaIndex = (cinemaCursor + c) % cinemas.Count;
                    var cinema = cinemas[cinemaIndex];
                    if (cinema.Rooms.Count == 0) continue;

                    var roomIds = cinema.Rooms.Select(r => r.Id).ToHashSet();

                    // Rạp này đã có suất chiếu trong khung giờ này -> thử rạp kế tiếp.
                    if (existing.Any(s => roomIds.Contains(s.RoomId)
                                          && s.StartTime >= start && s.StartTime < start.AddHours(1)))
                        continue;

                    // Luân phiên phim và tìm phòng còn trống cho đúng thời lượng phim.
                    for (var m = 0; m < movies.Count; m++)
                    {
                        var movieIndex = (movieCursor + m) % movies.Count;
                        var movie = movies[movieIndex];
                        if (date < movie.ReleaseDate.Date) continue;

                        var end = start.AddMinutes(movie.DurationMinutes);
                        var room = cinema.Rooms
                            .OrderBy(r => r.Id)
                            .FirstOrDefault(r => !existing.Any(s =>
                                s.RoomId == r.Id && s.StartTime < end && s.EndTime > start));

                        if (room == null) continue;

                        var showtime = new Showtime
                        {
                            MovieId = movie.Id,
                            RoomId = room.Id,
                            StartTime = start,
                            EndTime = end,
                            BasePrice = BasePriceFor(room.RoomType),
                            IsActive = true
                        };

                        db.Showtimes.Add(showtime);
                        existing.Add(showtime); // cập nhật bộ nhớ để không bị trùng trong cùng lượt
                        created++;

                        // Chuyển lượt sang rạp/phim kế tiếp.
                        cinemaCursor = (cinemaIndex + 1) % cinemas.Count;
                        movieCursor = (movieIndex + 1) % movies.Count;
                        placed = true;
                        break;
                    }
                }
            }
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }

    private static decimal BasePriceFor(RoomType roomType) => roomType switch
    {
        RoomType.IMAX => 220_000m,
        RoomType.Standard3D => 150_000m,
        _ => 100_000m
    };
}
