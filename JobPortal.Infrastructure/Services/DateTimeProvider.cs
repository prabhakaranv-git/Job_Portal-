using JobPortal.Application.Common.Interfaces;

namespace JobPortal.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
