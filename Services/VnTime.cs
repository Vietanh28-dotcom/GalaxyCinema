namespace GalaxyCinema.Services;

/// <summary>Giờ Việt Nam (UTC+7), hoạt động ổn định trên mọi múi giờ của máy chủ.</summary>
public static class VnTime
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateTime Today => Now.Date;

    private static TimeZoneInfo Resolve()
    {
        // Windows dùng "SE Asia Standard Time", Linux/macOS dùng "Asia/Ho_Chi_Minh".
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time", "Vietnam Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}
