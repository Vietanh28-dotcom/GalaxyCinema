using GalaxyCinema.Data;
using GalaxyCinema.Models;
using GalaxyCinema.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GalaxyCinema.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    private const int MaxSeatsPerBooking = 10;

    public BookingController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ---------- helpers ----------

    private bool IsShowtimeBookable(Showtime st)
        => st.IsActive && st.StartTime > VnTime.Now;

    private string GetLockSessionId()
    {
        if (HttpContext.Session.GetString("LockSessionId") is string sid && !string.IsNullOrEmpty(sid))
            return sid;
        var newId = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("LockSessionId", newId);
        return newId;
    }

    private string GetDraftKey(string key) => HttpContext.Session.GetString($"Draft_{key}") ?? string.Empty;
    private void SetDraft(string key, string value) => HttpContext.Session.SetString($"Draft_{key}", value);
    private void ClearDraft()
    {
        HttpContext.Session.Remove("Draft_ShowtimeId");
        HttpContext.Session.Remove("Draft_SeatIds");
        HttpContext.Session.Remove("Draft_Combos");
    }

    private List<int> GetDraftSeatIds()
    {
        var raw = GetDraftKey("SeatIds");
        if (string.IsNullOrEmpty(raw)) return new List<int>();
        return JsonSerializer.Deserialize<List<int>>(raw) ?? new List<int>();
    }

    private Dictionary<int, int> GetDraftComboQuantities()
    {
        var raw = GetDraftKey("Combos");
        if (string.IsNullOrEmpty(raw)) return new Dictionary<int, int>();
        return JsonSerializer.Deserialize<Dictionary<int, int>>(raw) ?? new Dictionary<int, int>();
    }

    private decimal SeatPrice(Showtime st, Seat seat) => st.BasePrice * seat.PriceMultiplier;

    private async Task CleanupExpiredLocksAsync()
    {
        var expired = await _db.SeatLocks.Where(l => l.ExpiresAt < VnTime.Now).ToListAsync();
        if (expired.Count > 0)
        {
            _db.SeatLocks.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
    }

    private async Task<bool> IsSeatBookedAsync(int showtimeId, int seatId)
    {
        return await _db.BookingSeats.AnyAsync(bs =>
            bs.SeatId == seatId &&
            bs.Booking.ShowtimeId == showtimeId &&
            bs.Booking.Status != BookingStatus.Cancelled);
    }

    private async Task<bool> DraftSeatsLockedAsync(int showtimeId, List<int> seatIds)
    {
        if (seatIds.Count == 0) return false;
        var sessionId = GetLockSessionId();
        foreach (var seatId in seatIds)
        {
            var lockRow = await _db.SeatLocks.FirstOrDefaultAsync(l => l.ShowtimeId == showtimeId && l.SeatId == seatId);
            if (lockRow == null || lockRow.SessionId != sessionId || lockRow.ExpiresAt < VnTime.Now) return false;
        }
        return true;
    }

    // ---------- Step 1: chọn rạp / ngày / suất chiếu ----------

    public async Task<IActionResult> SelectShowtime(int movieId, int? cinemaId, DateTime? date, int? format)
    {
        var movie = await _db.Movies.FindAsync(movieId);
        if (movie == null) return NotFound();

        var cinemas = await _db.Cinemas.ToListAsync();

        var future = VnTime.Now;

        var showtimes = await _db.Showtimes
            .Include(st => st.Room).ThenInclude(r => r.Cinema)
            .Where(st => st.MovieId == movieId && st.IsActive && st.StartTime > future)
            .OrderBy(st => st.StartTime)
            .ToListAsync();

        // Nhóm suất chiếu theo rạp
        var showtimesByCinema = showtimes
            .GroupBy(st => st.Room.CinemaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Mặc định chọn rạp đầu tiên có suất chiếu
        var selectedCinemaId = cinemaId ?? cinemas.Where(c => showtimesByCinema.ContainsKey(c.Id))
            .Select(c => c.Id).FirstOrDefault();

        // Các ngày có suất chiếu trong 7 ngày tới (dành riêng cho rạp đã chọn)
        var availableDates = showtimes
            .Where(st => st.Room.CinemaId == selectedCinemaId)
            .Select(st => st.StartTime.Date)
            .Distinct()
            .Where(d => d >= VnTime.Today && d <= VnTime.Today.AddDays(6))
            .OrderBy(d => d)
            .ToList();

        var selectedDate = date?.Date ?? (availableDates.Contains(VnTime.Today) ? VnTime.Today : availableDates.FirstOrDefault());

        var list = showtimesByCinema.TryGetValue(selectedCinemaId, out var all)
            ? all
            : new List<Showtime>();

        var dateFiltered = selectedDate == default
            ? list
            : list.Where(st => st.StartTime.Date == selectedDate).ToList();

        var availableFormats = dateFiltered
            .Select(st => (int)st.Room.RoomType)
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        Showtime[] finalList;
        if (format.HasValue && availableFormats.Contains(format.Value))
        {
            finalList = dateFiltered.Where(st => (int)st.Room.RoomType == format.Value).ToArray();
        }
        else
        {
            finalList = dateFiltered.ToArray();
            format = null;
        }

        return View(new SelectShowtimeViewModel
        {
            Movie = movie,
            Cinemas = cinemas,
            SelectedCinemaId = selectedCinemaId,
            SelectedDate = selectedDate == default ? null : selectedDate,
            AvailableDates = availableDates,
            Showtimes = finalList.ToList(),
            Format = format,
            AvailableFormats = availableFormats
        });
    }

    // ---------- Step 2: sơ đồ ghế + AJAX ----------

    [HttpGet]
    public async Task<IActionResult> SelectSeats(int showtimeId)
    {
        await CleanupExpiredLocksAsync();
        var showtime = await _db.Showtimes
            .Include(st => st.Movie)
            .Include(st => st.Room).ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(st => st.Id == showtimeId);
        if (showtime == null) return NotFound();
        if (!IsShowtimeBookable(showtime))
        {
            TempData["SeatError"] = "Suất chiếu này không còn khả dụng để đặt vé.";
            return RedirectToAction(nameof(SelectShowtime), new { movieId = showtime.MovieId });
        }

        var seats = await _db.Seats.Where(s => s.RoomId == showtime.RoomId).ToListAsync();

        var occupied = await _db.BookingSeats
            .Where(bs => bs.Booking.ShowtimeId == showtimeId && bs.Booking.Status != BookingStatus.Cancelled)
            .Select(bs => bs.SeatId)
            .ToListAsync();

        var sessionId = GetLockSessionId();
        var myLocks = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId && l.SessionId == sessionId)
            .Select(l => l.SeatId)
            .ToListAsync();

        var otherLocks = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId && l.SessionId != sessionId)
            .Select(l => l.SeatId)
            .ToListAsync();

        var rows = seats.GroupBy(s => s.RowNumber).OrderBy(g => g.Key).ToList();
        var maxCols = seats.Count > 0 ? seats.Max(s => s.ColumnNumber) : 0;

        var vm = new SeatMapViewModel
        {
            Showtime = showtime,
            Rows = rows,
            OccupiedSeatIds = occupied.ToHashSet(),
            MyLockedSeatIds = myLocks.ToHashSet(),
            OtherLockedSeatIds = otherLocks.ToHashSet(),
            MaxColumns = maxCols
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockSeat(int showtimeId, int seatId)
    {
        await CleanupExpiredLocksAsync();

        var showtime = await _db.Showtimes.FindAsync(showtimeId);
        var seat = await _db.Seats.FindAsync(seatId);
        if (showtime == null || seat == null || seat.RoomId != showtime.RoomId)
            return Json(new { success = false, message = "Suất chiếu hoặc ghế không hợp lệ." });

        if (!IsShowtimeBookable(showtime))
            return Json(new { success = false, message = "Suất chiếu này không còn khả dụng." });

        if (await IsSeatBookedAsync(showtimeId, seatId))
            return Json(new { success = false, message = $"Ghế {seat.Code} đã được đặt." });

        var sessionId = GetLockSessionId();
        var currentLockCount = await _db.SeatLocks.CountAsync(l => l.ShowtimeId == showtimeId && l.SessionId == sessionId);
        if (currentLockCount >= MaxSeatsPerBooking)
            return Json(new { success = false, message = $"Bạn chỉ được giữ tối đa {MaxSeatsPerBooking} ghế mỗi lượt." });

        var conflicting = await _db.SeatLocks
            .FirstOrDefaultAsync(l => l.ShowtimeId == showtimeId && l.SeatId == seatId && l.SessionId != sessionId);
        if (conflicting != null)
            return Json(new { success = false, message = $"Ghế {seat.Code} đang được khách khác chọn." });

        var existing = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId && l.SeatId == seatId)
            .ToListAsync();
        _db.SeatLocks.RemoveRange(existing);
        _db.SeatLocks.Add(new SeatLock
        {
            ShowtimeId = showtimeId,
            SeatId = seatId,
            SessionId = sessionId,
            LockedAt = VnTime.Now,
            ExpiresAt = VnTime.Now.Add(LockDuration)
        });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Json(new { success = false, message = $"Ghế {seat.Code} vừa được khách khác chọn, xin thử lại." });
        }

        return Json(new { success = true, message = $"Đã chọn ghế {seat.Code}.", price = SeatPrice(showtime, seat) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseSeat(int showtimeId, int seatId)
    {
        var sessionId = GetLockSessionId();
        var locks = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId && l.SeatId == seatId && l.SessionId == sessionId)
            .ToListAsync();
        if (locks.Count > 0)
        {
            _db.SeatLocks.RemoveRange(locks);
            await _db.SaveChangesAsync();
        }
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetSeatStatus(int showtimeId)
    {
        await CleanupExpiredLocksAsync();

        var showtime = await _db.Showtimes.FindAsync(showtimeId);
        if (showtime == null || !IsShowtimeBookable(showtime)) return Json(new { seats = Array.Empty<object>() });

        var seats = await _db.Seats.Where(s => s.RoomId == showtime.RoomId).ToListAsync();

        var booked = (await _db.BookingSeats
            .Where(bs => bs.Booking.ShowtimeId == showtimeId && bs.Booking.Status != BookingStatus.Cancelled)
            .Select(bs => bs.SeatId).ToListAsync()).ToHashSet();

        var locks = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId)
            .ToListAsync();
        var mySession = GetLockSessionId();

        var result = seats.Select(s =>
        {
            string state = "available";
            if (booked.Contains(s.Id)) state = "booked";
            else
            {
                var lockRow = locks.FirstOrDefault(l => l.SeatId == s.Id);
                if (lockRow != null)
                    state = lockRow.SessionId == mySession ? "mine" : "locked";
            }
            return new SeatStatusDto
            {
                Id = s.Id,
                Code = s.Code,
                State = state
            };
        }).ToList();

        return Json(new { seats = result });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectSeats(int showtimeId, int[] seatIds)
    {
        seatIds = seatIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (seatIds.Length == 0)
        {
            TempData["SeatError"] = "Vui lòng chọn ít nhất một ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }
        if (seatIds.Length > MaxSeatsPerBooking)
        {
            TempData["SeatError"] = $"Bạn chỉ được đặt tối đa {MaxSeatsPerBooking} ghế mỗi lượt.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }
        await CleanupExpiredLocksAsync();

        var showtime = await _db.Showtimes
            .Include(st => st.Room)
            .FirstOrDefaultAsync(st => st.Id == showtimeId);
        if (showtime == null) return NotFound();
        if (!IsShowtimeBookable(showtime))
        {
            TempData["SeatError"] = "Suất chiếu này không còn khả dụng để đặt vé.";
            return RedirectToAction(nameof(SelectShowtime), new { movieId = showtime.MovieId });
        }

        var seats = await _db.Seats.Where(s => s.RoomId == showtime.RoomId && seatIds.Contains(s.Id)).ToListAsync();

        if (seats.Count != seatIds.Length)
        {
            TempData["SeatError"] = "Có ghế không hợp lệ, vui lòng chọn lại.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }

        var sessionId = GetLockSessionId();
        var conflictSeats = new List<string>();

        foreach (var seat in seats)
        {
            if (await IsSeatBookedAsync(showtimeId, seat.Id))
            {
                conflictSeats.Add(seat.Code);
                continue;
            }
            var conflicting = await _db.SeatLocks
                .AnyAsync(l => l.ShowtimeId == showtimeId && l.SeatId == seat.Id && l.SessionId != sessionId);
            if (conflicting) conflictSeats.Add(seat.Code);
        }

        if (conflictSeats.Count > 0)
        {
            TempData["SeatError"] = $"Các ghế {string.Join(", ", conflictSeats)} đã có người chọn, vui lòng chọn lại.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }

        // Giữ khóa (mới) cho tất cả ghế đã chọn
        var existing = await _db.SeatLocks
            .Where(l => l.ShowtimeId == showtimeId && seats.Select(s => s.Id).Contains(l.SeatId))
            .ToListAsync();
        _db.SeatLocks.RemoveRange(existing);
        foreach (var seat in seats)
        {
            _db.SeatLocks.Add(new SeatLock
            {
                ShowtimeId = showtimeId,
                SeatId = seat.Id,
                SessionId = sessionId,
                LockedAt = VnTime.Now,
                ExpiresAt = VnTime.Now.Add(LockDuration)
            });
        }
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["SeatError"] = "Có người khác vừa chọn ghế, vui lòng thử lại.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }

        SetDraft("ShowtimeId", showtimeId.ToString());
        SetDraft("SeatIds", JsonSerializer.Serialize(seats.Select(s => s.Id).ToList()));

        return RedirectToAction(nameof(SelectCombo));
    }

    // ---------- Step 3: chọn combo ----------

    [HttpGet]
    public async Task<IActionResult> SelectCombo()
    {
        if (!int.TryParse(GetDraftKey("ShowtimeId"), out var showtimeId) || GetDraftSeatIds().Count == 0)
        {
            TempData["SeatError"] = "Phiên đặt vé đã hết hạn, vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId = 0 });
        }

        var showtime = await _db.Showtimes
            .Include(st => st.Movie)
            .Include(st => st.Room).ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(st => st.Id == showtimeId);
        if (showtime == null) return NotFound();
        if (!IsShowtimeBookable(showtime))
        {
            TempData["SeatError"] = "Suất chiếu này không còn khả dụng để đặt vé.";
            return RedirectToAction(nameof(SelectShowtime), new { movieId = showtime.MovieId });
        }

        var seatIds = GetDraftSeatIds();
        if (seatIds.Count == 0)
        {
            TempData["SeatError"] = "Phiên đặt vé đã hết hạn, vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }
        if (!await DraftSeatsLockedAsync(showtimeId, seatIds))
        {
            TempData["SeatError"] = "Phiên giữ ghế đã hết hạn hoặc ghế bị chiếm. Vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }
        var seats = await _db.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
        var combos = await _db.Combos.Where(c => c.IsActive).ToListAsync();

        return View(new SelectComboViewModel
        {
            Showtime = showtime,
            Combos = combos,
            SelectedSeats = seats.OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ToList(),
            SeatTotal = seats.Sum(s => SeatPrice(showtime, s)),
            SeatIds = seatIds
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectCombo(Dictionary<int, int> quantities)
    {
        if (!int.TryParse(GetDraftKey("ShowtimeId"), out var showtimeId))
            return RedirectToAction(nameof(SelectSeats), new { showtimeId = 0 });

        var showtime = await _db.Showtimes.FindAsync(showtimeId);
        if (showtime == null || !IsShowtimeBookable(showtime))
        {
            TempData["SeatError"] = "Suất chiếu này không còn khả dụng. Vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId });
        }

        var activeCombos = await _db.Combos.Where(c => c.IsActive).ToListAsync();
        var comboQuantities = new Dictionary<int, int>();
        foreach (var (comboId, qty) in quantities ?? new Dictionary<int, int>())
        {
            if (qty <= 0) continue;
            var combo = activeCombos.FirstOrDefault(c => c.Id == comboId);
            if (combo == null) continue;
            comboQuantities[comboId] = Math.Min(qty, 20);
        }

        SetDraft("Combos", JsonSerializer.Serialize(comboQuantities));
        return RedirectToAction(nameof(CustomerInfo));
    }

    // ---------- Step 4: thông tin khách hàng + xác nhận ----------

    [HttpGet]
    public async Task<IActionResult> CustomerInfo()
    {
        var vm = await BuildCustomerInfoVmAsync();
        if (vm == null)
        {
            TempData["SeatError"] = "Phiên đặt vé đã hết hạn, vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId = 0 });
        }
        return View(vm);
    }

    private async Task<CustomerInfoViewModel?> BuildCustomerInfoVmAsync()
    {
        if (!int.TryParse(GetDraftKey("ShowtimeId"), out var showtimeId)) return null;

        var showtime = await _db.Showtimes
            .Include(st => st.Movie)
            .Include(st => st.Room).ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(st => st.Id == showtimeId);
        if (showtime == null || !IsShowtimeBookable(showtime)) return null;

        var seatIds = GetDraftSeatIds();
        if (seatIds.Count == 0) return null;
        if (!await DraftSeatsLockedAsync(showtimeId, seatIds)) return null;
        if (!await _db.Seats.AnyAsync(s => seatIds.Contains(s.Id))) return null;

        var seats = await _db.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();

        var quantities = GetDraftComboQuantities();
        var activeCombos = await _db.Combos.Where(c => c.IsActive && quantities.Keys.Contains(c.Id)).ToListAsync();
        var combos = activeCombos
            .Select(c => new ComboLine
            {
                ComboId = c.Id,
                ComboName = c.Name,
                Price = c.Price,
                Quantity = quantities.TryGetValue(c.Id, out var q) ? q : 0
            })
            .Where(c => c.Quantity > 0)
            .ToList();

        var seatTotal = seats.Sum(s => SeatPrice(showtime, s));
        var comboTotal = combos.Sum(c => c.LineTotal);

        return new CustomerInfoViewModel
        {
            Showtime = showtime,
            SelectedSeats = seats.OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).ToList(),
            SeatTotal = seatTotal,
            ComboLines = combos,
            ComboTotal = comboTotal,
            TotalAmount = seatTotal + comboTotal,
            SeatIds = seatIds
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CustomerInfo(CustomerInfoViewModel model)
    {
        var vm = await BuildCustomerInfoVmAsync();
        if (vm == null)
        {
            TempData["SeatError"] = "Phiên đặt vé đã hết hạn, vui lòng chọn lại ghế.";
            return RedirectToAction(nameof(SelectSeats), new { showtimeId = 0 });
        }

        vm.CustomerName = model.CustomerName;
        vm.CustomerPhone = model.CustomerPhone;
        vm.CustomerEmail = model.CustomerEmail;
        vm.CustomerNote = model.CustomerNote;
        vm.PaymentMethod = model.PaymentMethod;

        if (!ModelState.IsValid)
        {
            foreach (var (k, v) in ModelState)
                if (v.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
                    Console.WriteLine($"[DBG-MS] key={k} errs={string.Join("|", v.Errors.Select(e => e.ErrorMessage))}");
            return View(vm);
        }

        await CleanupExpiredLocksAsync();
        var sessionId = GetLockSessionId();
        var seatIds = vm.SeatIds;

        var conflictSeats = new List<string>();
        foreach (var seatId in seatIds)
        {
            var seat = await _db.Seats.FindAsync(seatId);
            if (seat == null) { conflictSeats.Add("?"); continue; }
            if (await IsSeatBookedAsync(vm.Showtime.Id, seatId))
            {
                conflictSeats.Add(seat.Code);
                continue;
            }
            var lockRow = await _db.SeatLocks.FirstOrDefaultAsync(l =>
                l.ShowtimeId == vm.Showtime.Id && l.SeatId == seatId);
            if (lockRow == null || lockRow.SessionId != sessionId)
                conflictSeats.Add(seat.Code);
        }

        if (conflictSeats.Count > 0)
        {
            ViewBag.ConfirmError = $"Xin lỗi, các ghế {string.Join(", ", conflictSeats)} đã không còn khả dụng. Vui lòng quay lại chọn lại ghế.";
            return View(vm);
        }

        var bookingCode = "GC-" + VnTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(100, 999);
        var seatEntities = await _db.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
        var paymentMethod = vm.PaymentMethod;

        var booking = new Booking
        {
            BookingCode = bookingCode,
            ShowtimeId = vm.Showtime.Id,
            CustomerName = vm.CustomerName.Trim(),
            CustomerPhone = vm.CustomerPhone.Trim(),
            CustomerEmail = vm.CustomerEmail.Trim(),
            CustomerNote = vm.CustomerNote?.Trim(),
            SeatAmount = vm.SeatTotal,
            ComboAmount = vm.ComboTotal,
            TotalAmount = vm.TotalAmount,
            Status = BookingStatus.Pending,
            PaymentMethod = paymentMethod,
            CreatedAt = VnTime.Now,
            ComboSummary = vm.ComboLines.Count > 0
                ? string.Join("; ", vm.ComboLines.Select(c => $"{c.ComboName} x{c.Quantity}"))
                : null
        };

        var seatCodes = seatEntities.OrderBy(s => s.RowNumber).ThenBy(s => s.ColumnNumber).Select(s => s.Code);
        var payload = QrHelper.BuildTicketPayload(booking.BookingCode, vm.Showtime.Movie.Title,
            vm.Showtime.Room.Cinema.Name, vm.Showtime.Room.Name,
            vm.Showtime.StartTime.ToString("dd/MM/yyyy HH:mm"), string.Join(", ", seatCodes),
            booking.TotalAmount);
        booking.QrCodeBase64 = QrHelper.GenerateQrBase64(payload);

        foreach (var seat in seatEntities)
            booking.BookingSeats.Add(new BookingSeat { SeatId = seat.Id, Price = SeatPrice(vm.Showtime, seat) });

        using var transaction = await _db.Database.BeginTransactionAsync();

        // Kiểm tra lại lần cuối trong transaction (chống race-condition)
        var reCheckOk = true;
        foreach (var seatId in seatIds)
        {
            var bookedNow = await _db.BookingSeats.AnyAsync(bs =>
                bs.SeatId == seatId &&
                bs.Booking.ShowtimeId == vm.Showtime.Id &&
                bs.Booking.Status != BookingStatus.Cancelled);
            var lockNow = await _db.SeatLocks.FirstOrDefaultAsync(l =>
                l.ShowtimeId == vm.Showtime.Id && l.SeatId == seatId);
            if (bookedNow || lockNow == null || lockNow.SessionId != sessionId)
            {
                reCheckOk = false;
                break;
            }
        }

        if (!reCheckOk)
        {
            await transaction.RollbackAsync();
            ViewBag.ConfirmError = "Xin lỗi, có ghế vừa bị khách khác đặt. Vui lòng chọn lại ghế.";
            return View(vm);
        }

        _db.Bookings.Add(booking);
        var locks = await _db.SeatLocks
            .Where(l => l.ShowtimeId == vm.Showtime.Id && seatIds.Contains(l.SeatId))
            .ToListAsync();
        _db.SeatLocks.RemoveRange(locks);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        ClearDraft();
        if (paymentMethod == PaymentMethod.Online)
            return RedirectToAction(nameof(Payment), new { code = bookingCode });
        return RedirectToAction(nameof(Confirmation), new { code = bookingCode });
    }

    // ---------- Thanh toán online ----------

    [HttpGet]
    public async Task<IActionResult> Payment(string code)
    {
        var booking = await _db.Bookings
            .Include(b => b.Showtime).ThenInclude(st => st.Movie)
            .Include(b => b.Showtime).ThenInclude(st => st.Room).ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking == null) return NotFound();

        if (booking.PaymentMethod != PaymentMethod.Online)
            return RedirectToAction(nameof(Confirmation), new { code });

        if (booking.Status == BookingStatus.Paid)
            return RedirectToAction(nameof(Confirmation), new { code });

        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            var userEmail = (user?.Email ?? user?.UserName)?.Trim();
            var ownerEmail = booking.CustomerEmail?.Trim();
            if (string.IsNullOrEmpty(userEmail) || !string.Equals(userEmail, ownerEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentConfirmed(string code)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking == null) return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            var userEmail = (user?.Email ?? user?.UserName)?.Trim();
            var ownerEmail = booking.CustomerEmail?.Trim();
            if (string.IsNullOrEmpty(userEmail) || !string.Equals(userEmail, ownerEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        if (booking.Status == BookingStatus.Pending && booking.PaymentMethod == PaymentMethod.Online)
        {
            booking.Status = BookingStatus.Paid;
            _db.Entry(booking).State = EntityState.Modified;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Confirmation), new { code });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(string code)
    {
        var booking = await _db.Bookings
            .Include(b => b.Showtime).ThenInclude(st => st.Movie)
            .Include(b => b.Showtime).ThenInclude(st => st.Room).ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .FirstOrDefaultAsync(b => b.BookingCode == code);
        if (booking == null) return NotFound();

        if (!User.IsInRole("Admin"))
        {
            var user = await _userManager.GetUserAsync(User);
            var userEmail = (user?.Email ?? user?.UserName)?.Trim();
            var ownerEmail = booking.CustomerEmail?.Trim();
            if (string.IsNullOrEmpty(userEmail) || !string.Equals(userEmail, ownerEmail, StringComparison.OrdinalIgnoreCase))
                return Forbid();
        }

        return View(booking);
    }
}