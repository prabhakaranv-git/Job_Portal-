using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Authorize(Policy = "ApprovedRecruiterOnly")]
public class RecruiterController : BaseApiController
{
    private readonly IAuthService _authService;
    private readonly IJobService _jobService;

    public RecruiterController(IAuthService authService, IJobService jobService)
    {
        _authService = authService;
        _jobService = jobService;
    }

    /// <summary>
    /// Gets the recruiter dashboard data (Approved recruiters only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recruiter profile dashboard</returns>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserProfileAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists all jobs created by the authenticated recruiter across all statuses (Approved recruiters only).
    /// </summary>
    /// <param name="query">Recruiter job query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of recruiter's jobs</returns>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<JobListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResponse<JobListItemResponse>>>> GetRecruiterJobs(
        [FromQuery] RecruiterJobQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.GetRecruiterJobsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details of a specific job owned by the authenticated recruiter (Approved recruiters only).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full job details</returns>
    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> GetRecruiterJobById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.GetRecruiterJobByIdAsync(id, cancellationToken);
        return Ok(result);
    }
}
