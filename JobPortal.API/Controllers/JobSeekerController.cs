using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Authorize(Roles = "JobSeeker")]
public class JobSeekerController : BaseApiController
{
    private readonly IAuthService _authService;

    public JobSeekerController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Gets the current job seeker's profile details.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job seeker profile</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserProfileAsync(cancellationToken);
        return Ok(result);
    }
}
