using QRCoder;

namespace GalaxyCinema.Services;

public static class QrHelper
{
    public static string GenerateQrBase64(string content)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(bytes);
    }

    public static string BuildTicketPayload(string bookingCode, string movie, string cinema, string room,
        string showTime, string seats, decimal total)
    {
        return string.Join("\n",
            "GALAXY CINEMA - VÉ XEM PHIM",
            $"Mã vé: {bookingCode}",
            $"Phim: {movie}",
            $"Rạp: {cinema} - {room}",
            $"Suất: {showTime}",
            $"Ghế: {seats}",
            $"Tổng tiền: {total:N0} VNĐ");
    }
}