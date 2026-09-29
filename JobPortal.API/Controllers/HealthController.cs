using JobPortal.Application.DTOs.Health;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

public class HealthController : BaseApiController
{
    private readonly IHealthService _healthService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IHealthService healthService, ILogger<HealthController> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    /// <summary>
    /// Gets application health and database connectivity status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HealthCheckResponse</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Health probe requested. TraceId: {TraceId}", TraceId);
        var health = await _healthService.GetHealthAsync(cancellationToken);
        return Ok(health);
    }
}
