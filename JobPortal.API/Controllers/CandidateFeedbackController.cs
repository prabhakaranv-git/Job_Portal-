using JobPortal.Application.DTOs.CandidateFeedback;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Route("api/v1/recruiter/applications/{applicationId:guid}/feedback")]
[Authorize(Policy = "ApprovedRecruiterOnly")]
public class CandidateFeedbackController : BaseApiController
{
    private readonly ICandidateFeedbackService _feedbackService;
    private readonly ILogger<CandidateFeedbackController> _logger;

    public CandidateFeedbackController(ICandidateFeedbackService feedbackService, ILogger<CandidateFeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _logger = logger;
    }

    /// <summary>
    /// Creates structured candidate evaluation feedback for an application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="request">Feedback evaluation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created candidate feedback</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CandidateFeedbackResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CandidateFeedbackResponse>>> CreateFeedback(
        [FromRoute] Guid applicationId,
        [FromBody] CreateCandidateFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _feedbackService.CreateFeedbackAsync(applicationId, request, cancellationToken);
        return CreatedAtAction(nameof(GetFeedback), new { applicationId }, result);
    }

    /// <summary>
    /// Retrieves candidate evaluation feedback for an application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Candidate feedback details</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CandidateFeedbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateFeedbackResponse>>> GetFeedback(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _feedbackService.GetFeedbackAsync(applicationId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates candidate evaluation feedback for an application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="request">Updated evaluation details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated candidate feedback details</returns>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<CandidateFeedbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateFeedbackResponse>>> UpdateFeedback(
        [FromRoute] Guid applicationId,
        [FromBody] UpdateCandidateFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _feedbackService.UpdateFeedbackAsync(applicationId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes candidate evaluation feedback for an application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteFeedback(
        [FromRoute] Guid applicationId,
        CancellationToken cancellationToken)
    {
        var result = await _feedbackService.DeleteFeedbackAsync(applicationId, cancellationToken);
        return Ok(result);
    }
}
