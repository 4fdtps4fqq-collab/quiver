namespace KiteFlow.Services.Academics.Api.Domain;

public enum LessonStatus
{
    Scheduled = 1,
    Confirmed = 2,
    Realized = 3,
    Rescheduled = 4,
    Cancelled = 5,
    CancelledByWind = 6,
    NoShow = 7
}
