using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected string? TraceId => HttpContext.Items["CorrelationId"]?.ToString() ?? HttpContext.TraceIdentifier;
}
