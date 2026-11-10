using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // ---- Identity: admin role + user ----
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));

        if (await userManager.FindByEmailAsync("admin@galaxy.vn") == null)
        {
            var admin = new IdentityUser
            {
                UserName = "admin@galaxy.vn",
                Email = "admin@galaxy.vn",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                await userManager.SetLockoutEnabledAsync(admin, true);
            }
        }
        else
        {
            var admin = await userManager.FindByEmailAsync("admin@galaxy.vn");
            if (admin != null)
                await userManager.SetLockoutEnabledAsync(admin, true);
        }

        if (await db.Movies.AnyAsync()) return; // already seeded

        // ---- Movies ----
        var movies = new List<Movie>
        {
            new Movie
            {
                Title = "Cuộc Chiến Liên Ngân Hà",
                Description = "Khi một thế lực cổ đại từ rìa vũ trụ thức tỉnh, nhóm chiến binh cuối cùng của Ngân Hà phải hợp sức bảo vệ viên tinh thạch giữ cho vũ trụ không sụp đổ. Một hành trình với vô số pha chiến đấu trên không gian.",
                Genre = "Hành động, Khoa học viễn tưởng",
                DurationMinutes = 148, Director = "Aiden Vũ", Cast = "Hải Long, Minh Châu, Đăng Khoa, Ngọc Trinh",
                PosterUrl = "/img/posters/poster1.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 8.7, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(-10), IsFeatured = true
            },
            new Movie
            {
                Title = "Siêu Anh Hùng: Bình Minh Mới",
                Description = "Một thế hệ siêu anh hùng mới xuất hiện sau cuộc khủng hoảng năng lượng toàn cầu. Liệu họ có thể gánh vác trọng trách bảo vệ Trái Đất trước cuộc tấn công từ chiều không gian khác?",
                Genre = "Hành động, Phiêu lưu",
                DurationMinutes = 132, Director = "Lucas Phan", Cast = "Quốc Bảo, Thảo Vy, Duy Anh",
                PosterUrl = "/img/posters/poster2.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 8.2, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(-6), IsFeatured = true
            },
            new Movie
            {
                Title = "Chiến Binh Đa Vũ Trụ",
                Description = "Những siêu chiến binh xuyên qua vô số vũ trụ song song để ngăn chặn một thảm họa có thể xóa sổ mọi thế giới cùng lúc.",
                Genre = "Hành động, Hài",
                DurationMinutes = 126, Director = "Mai Trang", Cast = "Đức Phát, Linh Đan, Hoàng Nam",
                PosterUrl = "/img/posters/poster3.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 7.9, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(-3)
            },
            new Movie
            {
                Title = "Ánh Sáng Cuối Ngân Hà",
                Description = "Tàu vũ trụ Kền Kền gặp sự cố giữa chặng đường săn sao chổi. Bốn phi hành gia bị lạc trong tĩnh lặng của không gian, nơi chỉ còn ánh sáng cuối cùng của một ngôi sao sắp tàn.",
                Genre = "Khoa học viễn tưởng, Chính kịch",
                DurationMinutes = 118, Director = "Trần Huy", Cast = "Hà My, Gia Bảo, Tuấn Kiệt",
                PosterUrl = "/img/posters/poster4.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 8.4, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(-1)
            },
            new Movie
            {
                Title = "Kẻ Săn Hành Tinh",
                Description = "Một cựu đặc vụ liên hành tinh truy đuổi kẻ đào thoát mang theo mầm bệnh có thể biến cả hành tinh thành tro bụi.",
                Genre = "Hành động, Giật gân",
                DurationMinutes = 112, Director = "Ngô Đức", Cast = "Mạnh Quân, Thu Hằng",
                PosterUrl = "/img/posters/poster5.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 7.5, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(-2)
            },
            new Movie
            {
                Title = "Cổng Nhảy Không Gian",
                Description = "Phát hiện cổng dịch chuyển đầu tiên của nhân loại, đoàn thám hiểm mở ra cánh cửa tới một hệ sao kỳ lạ - nhưng họ không đơn độc ở bên kia.",
                Genre = "Phiêu lưu, Khoa học viễn tưởng",
                DurationMinutes = 135, Director = "Kim Ánh", Cast = "Thành Đạt, Phương Linh, Vũ Long",
                PosterUrl = "/img/posters/poster6.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 8.0, Status = MovieStatus.NowShowing, ReleaseDate = VnTime.Today.AddDays(0)
            },
            new Movie
            {
                Title = "Tia Năng Lượng Xanh",
                Description = "Một nhà khoa học trẻ vô tình hấp thụ nguồn năng lượng vũ trụ và trở thành mục tiêu săn đuổi của cả phe thiện lẫn phe ác.",
                Genre = "Khoa học viễn tưởng",
                DurationMinutes = 124, Director = "Lê Bình", Cast = "Xuân Mai, Đình Khôi",
                PosterUrl = "/img/posters/poster7.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 0, Status = MovieStatus.ComingSoon, ReleaseDate = VnTime.Today.AddDays(14)
            },
            new Movie
            {
                Title = "Ngôi Sao Băng Đêm",
                Description = "Đêm định mệnh khi ngôi sao băng rơi xuống thành phố, chạm vào trái tim của hai con người xa lạ và thay đổi cuộc đời họ mãi mãi.",
                Genre = "Lãng mạn, Chính kịch",
                DurationMinutes = 105, Director = "Vũ Hạnh", Cast = "Nhã Phương, Minh Tú",
                PosterUrl = "/img/posters/poster8.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 0, Status = MovieStatus.ComingSoon, ReleaseDate = VnTime.Today.AddDays(21)
            },
            new Movie
            {
                Title = "Nữ Hoàng Sao Hỏa",
                Description = "Sử thi vũ trụ kể về nữ tướng dẫn dắt đội quân khai hoang lập nên đế chế đầu tiên trên Sao Hỏa giữa những cơn bão cát và tranh giành quyền lực.",
                Genre = "Sử thi, Khoa học viễn tưởng",
                DurationMinutes = 152, Director = "Hoàng Dũng", Cast = "Diệu Hân, Công Minh, Bảo Ngọc",
                PosterUrl = "/img/posters/poster9.svg", TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                Rating = 0, Status = MovieStatus.ComingSoon, ReleaseDate = VnTime.Today.AddDays(28)
            }
        };

        db.Movies.AddRange(movies);
        await db.SaveChangesAsync();

        // ---- Cinemas + Rooms + Seats ----
        var cinema1 = new Cinema
        {
            Name = "Galaxy Cinema Hoàng Văn Thụ",
            Address = "247 Hoàng Văn Thụ, Q. Tân Bình, TP. Hồ Chí Minh",
            Phone = "028 3948 8118"
        };
        var cinema2 = new Cinema
        {
            Name = "Galaxy Cinema Nguyễn Trãi",
            Address = "Số 2 Nguyễn Trãi, Q. Thanh Xuân, Hà Nội",
            Phone = "024 3555 0600"
        };
        db.Cinemas.AddRange(cinema1, cinema2);
        await db.SaveChangesAsync();

        var layouts = new[]
        {
            new { Name = "Phòng 2D - Sao Đôi", Type = RoomType.Standard2D, Rows = 7, Cols = 10, VipRows = 1, CoupleRows = 1 },
            new { Name = "Phòng 3D - Tinh Vân", Type = RoomType.Standard3D, Rows = 8, Cols = 12, VipRows = 1, CoupleRows = 1 },
            new { Name = "Phòng IMAX - Thiên Hà", Type = RoomType.IMAX, Rows = 10, Cols = 16, VipRows = 2, CoupleRows = 1 }
        };

        foreach (var cinema in new[] { cinema1, cinema2 })
        {
            foreach (var layout in layouts)
            {
                var room = new Room
                {
                    Cinema = cinema,
                    Name = layout.Name,
                    RoomType = layout.Type,
                    Capacity = layout.Rows * layout.Cols
                };
                db.Rooms.Add(room);
                await db.SaveChangesAsync();

                for (int row = 1; row <= layout.Rows; row++)
                {
                    var seatType = SeatType.Normal;
                    decimal multiplier = 1m;
                    if (row > layout.Rows - layout.CoupleRows) { seatType = SeatType.Couple; multiplier = 2m; }
                    else if (row > layout.Rows - layout.CoupleRows - layout.VipRows) { seatType = SeatType.Vip; multiplier = 1.5m; }

                    for (int col = 1; col <= layout.Cols; col++)
                    {
                        var code = $"{Convert.ToChar('A' + row - 1)}{col}";
                        db.Seats.Add(new Seat
                        {
                            RoomId = room.Id,
                            Code = code,
                            RowNumber = row,
                            ColumnNumber = col,
                            SeatType = seatType,
                            PriceMultiplier = multiplier
                        });
                    }
                }
                await db.SaveChangesAsync();
            }
        }

        // ---- Combos ----
        var combos = new List<Combo>
        {
            new Combo { Name = "Combo Bắp Ngô Siêu To", Description = "Bắp ngô bơ 85oz + Pepsi 22oz", Price = 89000, ImageUrl = "/img/combos/combo1.svg", IsActive = true },
            new Combo { Name = "Combo Đôi Thiên Hà", Description = "2 bắp ngô 64oz + 2 nước 22oz", Price = 139000, ImageUrl = "/img/combos/combo2.svg", IsActive = true },
            new Combo { Name = "Combo Ngọt Ngào", Description = "Bắp caramel + bắp vị socola + nước", Price = 109000, ImageUrl = "/img/combos/combo3.svg", IsActive = true },
            new Combo { Name = "Nước Ngọt Riêng Lẻ", Description = "Pepsi / 7Up / Mirinda 22oz", Price = 39000, ImageUrl = "/img/combos/combo4.svg", IsActive = true }
        };
        db.Combos.AddRange(combos);
        await db.SaveChangesAsync();

        // ---- Showtimes: tự động chia đều các rạp, mỗi suất cách 1 tiếng ----
        await ShowtimePlanner.GenerateAsync(db, VnTime.Now);

        // ---- Sample bookings for today's first showtimes ----
        var todayShowtimes = db.Showtimes
            .Include(s => s.Room)
            .Include(s => s.Movie)
            .Where(s => s.StartTime.Date == VnTime.Today)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (todayShowtimes.Any())
        {
            SeedBooking(db, todayShowtimes, 0, "Nguyễn Văn An", "0912345678", "an.nguyen@gmail.com", new[] { "A1", "A2" }, new int[] { combos[0].Id });
            SeedBooking(db, todayShowtimes, 1, "Trần Thị Bích", "0908123456", "bich.tran@gmail.com", new[] { "B1" }, Array.Empty<int>());
            await db.SaveChangesAsync();
        }
    }

    private static void SeedBooking(ApplicationDbContext db, List<Showtime> todayShowtimes, int index,
        string name, string phone, string email, string[] seatCodes, int[] comboIds)
    {
        var showtime = todayShowtimes[index % todayShowtimes.Count];
        var seats = db.Seats.Where(s => s.RoomId == showtime.RoomId && seatCodes.Contains(s.Code)).ToList();
        if (!seats.Any()) return;

        var booking = new Booking
        {
            BookingCode = $"BK{VnTime.Now.Ticks % 100000}",
            ShowtimeId = showtime.Id,
            CustomerName = name,
            CustomerPhone = phone,
            CustomerEmail = email,
            CreatedAt = VnTime.Now,
            Status = BookingStatus.Paid
        };

        decimal seatAmount = 0;
        foreach (var seat in seats)
        {
            var price = showtime.BasePrice * seat.PriceMultiplier;
            booking.BookingSeats.Add(new BookingSeat { SeatId = seat.Id, Price = price });
            seatAmount += price;
        }

        decimal comboAmount = 0;
        if (comboIds != null && comboIds.Length > 0)
        {
            var combos = db.Combos.Where(c => comboIds.Contains(c.Id)).ToList();
            comboAmount = combos.Sum(c => c.Price);
            booking.ComboSummary = string.Join(", ", combos.Select(c => c.Name));
        }

        booking.SeatAmount = seatAmount;
        booking.ComboAmount = comboAmount;
        booking.TotalAmount = seatAmount + comboAmount;

        db.Bookings.Add(booking);
    }
}
