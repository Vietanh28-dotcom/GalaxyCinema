using GalaxyCinema.Data;
using Microsoft.EntityFrameworkCore;

namespace GalaxyCinema.Services;

/// <summary>
/// Tự động tạo suất chiếu cho tất cả phim đang chiếu theo ngày giờ hiện tại của Việt Nam.
/// Mỗi suất cách nhau 1 tiếng và được chia đều cho các rạp. Chạy ngay khi ứng dụng khởi động
/// và lặp lại mỗi 30 phút nên lịch chiếu luôn được cập nhật sang ngày mới.
/// </summary>
public class ShowtimeAutoScheduler : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShowtimeAutoScheduler> _logger;

    public ShowtimeAutoScheduler(IServiceScopeFactory scopeFactory, ILogger<ShowtimeAutoScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ ứng dụng khởi động xong (migration + seed) rồi mới tạo lịch.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var created = await GenerateAsync(stoppingToken);
                if (created > 0)
                    _logger.LogInformation("Tự động tạo {Count} suất chiếu mới theo giờ Việt Nam.", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tự động tạo suất chiếu.");
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Tạo các suất chiếu còn thiếu và trả về số suất đã tạo.</summary>
    public async Task<int> GenerateAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await ShowtimePlanner.GenerateAsync(db, VnTime.Now, cancellationToken);
    }
}
