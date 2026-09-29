using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Authorize(Policy = "ApprovedRecruiterOnly")]
public class RecruiterApplicationsController : BaseApiController
{
    private readonly IJobApplicationService _applicationService;
    private readonly ILogger<RecruiterApplicationsController> _logger;

    public RecruiterApplicationsController(IJobApplicationService applicationService, ILogger<RecruiterApplicationsController> logger)
    {
        _applicationService = applicationService;
        _logger = logger;
    }

    /// <summary>
    /// Lists all applications for a specific job owned by the authenticated approved recruiter.
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="query">Pagination and status filter query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of job applications</returns>
    [HttpGet("/api/v1/recruiter/jobs/{jobId:guid}/applications")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ApplicationListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResponse<ApplicationListItemResponse>>>> GetJobApplications(
        [FromRoute] Guid jobId,
        [FromQuery] RecruiterApplicationQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetRecruiterJobApplicationsAsync(jobId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves application details and applicant information for a job owned by the recruiter.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Application details</returns>
    [HttpGet("/api/v1/recruiter/applications/{applicationId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ApplicationListItemResponse>>> GetApplicationById(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetRecruiterApplicationByIdAsync(applicationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Downloads the resume attached to an application submitted for a job owned by the recruiter.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resume file stream</returns>
    [HttpGet("/api/v1/recruiter/applications/{applicationId:guid}/resume")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadApplicantResume(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var downloadDto = await _applicationService.DownloadApplicantResumeAsync(applicationId, cancellationToken);
        return File(downloadDto.FileStream, downloadDto.ContentType, downloadDto.DownloadFileName);
    }

    /// <summary>
    /// Updates the pipeline status stage of an application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="request">New application status stage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated application details</returns>
    [HttpPatch("/api/v1/recruiter/applications/{applicationId:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> UpdateApplicationStatus(
        [FromRoute] Guid applicationId,
        [FromBody] UpdateApplicationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.UpdateApplicationStatusAsync(applicationId, request, cancellationToken);
        return Ok(result);
    }
}
