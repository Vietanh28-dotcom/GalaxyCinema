using System.Text.RegularExpressions;
using GalaxyCinema.Models;

namespace GalaxyCinema.Helpers;

public static class MapHelper
{
    private static readonly Regex RxAt = new(@"@(-?\d{1,3}\.\d+),(-?\d{1,3}\.\d+)", RegexOptions.Compiled);
    private static readonly Regex RxData34 = new(@"!3d(-?\d{1,3}\.\d+)!4d(-?\d{1,3}\.\d+)", RegexOptions.Compiled);
    private static readonly Regex RxQuery = new(@"[?&](?:q|ll|cbll)=(-?\d{1,3}\.\d+),(-?\d{1,3}\.\d+)", RegexOptions.Compiled);
    private const double Spread = 0.012;

    public static string GetEmbedSrc(Cinema cinema)
    {
        var url = cinema.MapEmbedUrl?.Trim();

        if (!string.IsNullOrWhiteSpace(url) && TryExtractCoords(url, out var lat, out var lng))
        {
            return BuildOsmEmbed(lat, lng);
        }

        return BuildOsmEmbed(10.7769, 106.7009);
    }

    public static string GetMapLink(Cinema cinema)
    {
        var url = cinema.MapEmbedUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return url;
        if (!string.IsNullOrWhiteSpace(cinema.Address))
            return $"https://www.google.com/maps/search/{Uri.EscapeDataString(cinema.Address)}";
        return "https://www.google.com/maps";
    }

    private static bool TryExtractCoords(string url, out double lat, out double lng)
    {
        lat = lng = 0;

        if (RxAt.IsMatch(url))
        {
            var m = RxAt.Match(url);
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lat) &&
                double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lng))
                return true;
        }

        if (RxData34.IsMatch(url))
        {
            var m = RxData34.Match(url);
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lat) &&
                double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lng))
                return true;
        }

        if (RxQuery.IsMatch(url))
        {
            var m = RxQuery.Match(url);
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lat) &&
                double.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out lng))
                return true;
        }

        return false;
    }

    private static string BuildOsmEmbed(double lat, double lng)
    {
        var left = (lng - Spread).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var bottom = (lat - Spread).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var right = (lng + Spread).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var top = (lat + Spread).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var latS = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lngS = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"https://www.openstreetmap.org/export/embed.html?bbox={left},{bottom},{right},{top}&layer=mapnik&marker={latS},{lngS}";
    }
}
