using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

public class JobsController : BaseApiController
{
    private readonly IJobService _jobService;
    private readonly IJobApplicationService _applicationService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobService jobService,
        IJobApplicationService applicationService,
        ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _applicationService = applicationService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a job application for a published active job (Job Seeker only).
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="request">Application details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Submitted application details</returns>
    [HttpPost("{jobId:guid}/apply")]
    [Authorize(Roles = "JobSeeker")]
    [ProducesResponseType(typeof(ApiResponse<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ApplicationResponse>>> ApplyToJob(
        [FromRoute] Guid jobId,
        [FromBody] ApplyJobRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.ApplyToJobAsync(jobId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new job posting in Draft status (Approved recruiters only).
    /// </summary>
    /// <param name="request">Job creation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created job posting</returns>
    [HttpPost]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> CreateJob(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.CreateJobAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetJobById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Searches and filters active, published job postings with pagination (Public).
    /// </summary>
    /// <param name="query">Search and filter query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of active jobs</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<JobListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResponse<JobListItemResponse>>>> SearchJobs(
        [FromQuery] JobSearchQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.SearchJobsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a published, active job posting by ID (Public).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job posting details</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> GetJobById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.GetJobByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates a job posting (Approved owning recruiter only).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="request">Updated job details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated job posting</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> UpdateJob(
        [FromRoute] Guid id,
        [FromBody] UpdateJobRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.UpdateJobAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Publishes a draft job posting making it publicly visible (Approved owning recruiter only).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Published job posting</returns>
    [HttpPut("{id:guid}/publish")]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> PublishJob(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.PublishJobAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Closes an active published job posting (Approved owning recruiter only).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Closed job posting</returns>
    [HttpPut("{id:guid}/close")]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> CloseJob(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.CloseJobAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Archives a job posting removing it permanently from active status (Approved owning recruiter only).
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Archived job posting</returns>
    [HttpPut("{id:guid}/archive")]
    [Authorize(Policy = "ApprovedRecruiterOnly")]
    [ProducesResponseType(typeof(ApiResponse<JobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<JobResponse>>> ArchiveJob(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _jobService.ArchiveJobAsync(id, cancellationToken);
        return Ok(result);
    }
}
