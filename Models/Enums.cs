namespace GalaxyCinema.Models;

public enum MovieStatus
{
    NowShowing = 1,
    ComingSoon = 2
}

public enum RoomType
{
    Standard2D = 1,
    Standard3D = 2,
    IMAX = 3
}

public enum SeatType
{
    Normal = 1,
    Vip = 2,
    Couple = 3
}

public enum BookingStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}

public enum PaymentMethod
{
    AtCounter = 1,
    Online = 2
}