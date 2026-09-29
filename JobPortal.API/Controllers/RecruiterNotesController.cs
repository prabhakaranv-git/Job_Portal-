using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.RecruiterNote;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Route("api/v1/recruiter/applications/{applicationId:guid}/notes")]
[Authorize(Policy = "ApprovedRecruiterOnly")]
public class RecruiterNotesController : BaseApiController
{
    private readonly IRecruiterNoteService _noteService;
    private readonly ILogger<RecruiterNotesController> _logger;

    public RecruiterNotesController(IRecruiterNoteService noteService, ILogger<RecruiterNotesController> logger)
    {
        _noteService = noteService;
        _logger = logger;
    }

    /// <summary>
    /// Creates an internal private note for a candidate application (Approved owning recruiter only).
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="request">Note content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created note metadata</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<RecruiterNoteResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecruiterNoteResponse>>> CreateNote(
        [FromRoute] Guid applicationId,
        [FromBody] CreateRecruiterNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _noteService.CreateNoteAsync(applicationId, request, cancellationToken);
        return CreatedAtAction(nameof(GetNoteById), new { applicationId, noteId = result.Data!.Id }, result);
    }

    /// <summary>
    /// Lists all internal notes for a candidate application with pagination.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="query">Pagination query parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of recruiter notes</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<RecruiterNoteResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResponse<RecruiterNoteResponse>>>> GetNotes(
        [FromRoute] Guid applicationId,
        [FromQuery] RecruiterNoteQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _noteService.GetNotesAsync(applicationId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific internal recruiter note by ID.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="noteId">Note ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Note details</returns>
    [HttpGet("{noteId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecruiterNoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecruiterNoteResponse>>> GetNoteById(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid noteId,
        CancellationToken cancellationToken)
    {
        var result = await _noteService.GetNoteByIdAsync(applicationId, noteId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing internal recruiter note.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="noteId">Note ID</param>
    /// <param name="request">Updated note content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated note details</returns>
    [HttpPut("{noteId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<RecruiterNoteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecruiterNoteResponse>>> UpdateNote(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid noteId,
        [FromBody] UpdateRecruiterNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _noteService.UpdateNoteAsync(applicationId, noteId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an internal recruiter note.
    /// </summary>
    /// <param name="applicationId">Application ID</param>
    /// <param name="noteId">Note ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deletion confirmation</returns>
    [HttpDelete("{noteId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNote(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid noteId,
        CancellationToken cancellationToken)
    {
        var result = await _noteService.DeleteNoteAsync(applicationId, noteId, cancellationToken);
        return Ok(result);
    }
}
