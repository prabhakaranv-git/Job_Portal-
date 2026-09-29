using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Route("api/v1/jobseeker/resumes")]
[Authorize(Roles = "JobSeeker")]
public class JobSeekerResumesController : BaseApiController
{
    private readonly IResumeService _resumeService;
    private readonly ILogger<JobSeekerResumesController> _logger;

    public JobSeekerResumesController(IResumeService resumeService, ILogger<JobSeekerResumesController> logger)
    {
        _resumeService = resumeService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a new resume document (PDF, DOC, DOCX - Job Seeker only).
    /// </summary>
    /// <param name="file">The resume document file</param>
    /// <param name="isDefault">Optional flag to mark this resume as the default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Uploaded resume metadata</returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ResumeResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ResumeResponse>>> UploadResume(
        IFormFile file,
        [FromForm] bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new JobPortal.Domain.Exceptions.ValidationException("File", "A non-empty file must be uploaded.");
        }

        await using var stream = file.OpenReadStream();
        var result = await _resumeService.UploadResumeAsync(
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            isDefault,
            cancellationToken);

        return CreatedAtAction(nameof(GetResumeById), new { resumeId = result.Data!.Id }, result);
    }

    /// <summary>
    /// Lists all resumes uploaded by the authenticated Job Seeker.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of resume metadata</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ResumeResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IEnumerable<ResumeResponse>>>> GetMyResumes(CancellationToken cancellationToken)
    {
        var result = await _resumeService.GetMyResumesAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves metadata for a specific resume owned by the authenticated Job Seeker.
    /// </summary>
    /// <param name="resumeId">Resume ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Resume metadata</returns>
    [HttpGet("{resumeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ResumeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResumeResponse>>> GetResumeById(
        [FromRoute] Guid resumeId,
        CancellationToken cancellationToken)
    {
        var result = await _resumeService.GetResumeByIdAsync(resumeId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Downloads the physical resume document file (Job Seeker owner only).
    /// </summary>
    /// <param name="resumeId">Resume ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File stream</returns>
    [HttpGet("{resumeId:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadResume(
        [FromRoute] Guid resumeId,
        CancellationToken cancellationToken)
    {
        var downloadDto = await _resumeService.DownloadResumeAsync(resumeId, cancellationToken);
        return File(downloadDto.FileStream, downloadDto.ContentType, downloadDto.DownloadFileName);
    }

    /// <summary>
    /// Sets a specified resume as the default resume for the authenticated Job Seeker.
    /// </summary>
    /// <param name="resumeId">Resume ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated resume metadata</returns>
    [HttpPatch("{resumeId:guid}/default")]
    [ProducesResponseType(typeof(ApiResponse<ResumeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ResumeResponse>>> SetDefaultResume(
        [FromRoute] Guid resumeId,
        CancellationToken cancellationToken)
    {
        var result = await _resumeService.SetDefaultResumeAsync(resumeId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a resume document and its physical file (Job Seeker owner only).
    /// </summary>
    /// <param name="resumeId">Resume ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete("{resumeId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteResume(
        [FromRoute] Guid resumeId,
        CancellationToken cancellationToken)
    {
        var result = await _resumeService.DeleteResumeAsync(resumeId, cancellationToken);
        return Ok(result);
    }
}
