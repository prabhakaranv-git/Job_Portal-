using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Route("api/v1/jobseeker/applications")]
[Authorize(Roles = "JobSeeker")]
public class JobSeekerApplicationsController : BaseApiController
{
    private readonly IJobApplicationService _applicationService;
    private readonly ILogger<JobSeekerApplicationsController> _logger;

    public JobSeekerApplicationsController(IJobApplicationService applicationService, ILogger<JobSeekerApplicationsController> logger)
    {
        _applicationService = applicationService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all job applications submitted by the authenticated Job Seeker with pagination and status filtering.
    /// </summary>
    /// <param name="query">Query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of applications</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ApplicationListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResponse<ApplicationListItemResponse>>>> GetMyApplications(
        [FromQuery] JobSeekerApplicationQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetJobSeekerApplicationsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details for a specific application submitted by the authenticated Job Seeker.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Application details</returns>
    [HttpGet("{applicationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> GetApplicationById(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetJobSeekerApplicationByIdAsync(applicationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Withdraws an existing active application (Job Seeker owner only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Withdrawn application details</returns>
    [HttpPatch("{applicationId:guid}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> WithdrawApplication(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.WithdrawApplicationAsync(applicationId, cancellationToken);
        return Ok(result);
    }
}
