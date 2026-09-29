using JobPortal.Application.DTOs.Health;

namespace JobPortal.Application.Interfaces;

public interface IHealthService
{
    Task<HealthCheckResponse> GetHealthAsync(CancellationToken cancellationToken = default);
}
