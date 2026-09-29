using System.Diagnostics;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Health;
using JobPortal.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class HealthService : IHealthService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthService> _logger;

    public HealthService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration,
        ILogger<HealthService> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var serviceName = _configuration["Application:Name"] ?? "JobPortal API";
        var serviceVersion = _configuration["Application:Version"] ?? "1.0.0";

        var response = new HealthCheckResponse
        {
            Service = serviceName,
            Version = serviceVersion,
            Timestamp = _dateTimeProvider.UtcNow,
            Status = "Healthy",
            Database = new DatabaseHealthDto()
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await _context.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            response.Database.CanConnect = canConnect;
            response.Database.ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);

            if (canConnect)
            {
                response.Database.Status = "Healthy";
                response.Database.Details = "Database connection verified successfully.";
            }
            else
            {
                response.Database.Status = "Unhealthy";
                response.Database.Details = "Unable to connect to PostgreSQL database.";
                response.Status = "Degraded";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Health check database connectivity probe failed.");
            response.Database.CanConnect = false;
            response.Database.ResponseTimeMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
            response.Database.Status = "Unhealthy";
            response.Database.Details = "Database probe failed or connection timeout.";
            response.Status = "Degraded";
        }

        return response;
    }
}
