using JobPortal.Application.DTOs.Admin;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPortal.API.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : BaseApiController
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// Approves a pending recruiter account (Admin only).
    /// </summary>
    /// <param name="recruiterId">Recruiter ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RecruiterApprovalResponseDto</returns>
    [HttpPut("recruiters/{recruiterId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<RecruiterApprovalResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecruiterApprovalResponseDto>>> ApproveRecruiter(
        [FromRoute] Guid recruiterId,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.ApproveRecruiterAsync(recruiterId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Rejects a recruiter account (Admin only).
    /// </summary>
    /// <param name="recruiterId">Recruiter ID</param>
    /// <param name="request">Rejection reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>RecruiterApprovalResponseDto</returns>
    [HttpPut("recruiters/{recruiterId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<RecruiterApprovalResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RecruiterApprovalResponseDto>>> RejectRecruiter(
        [FromRoute] Guid recruiterId,
        [FromBody] RejectRecruiterRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.RejectRecruiterAsync(recruiterId, request?.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists all recruiters pending administrative approval (Admin only).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending recruiters</returns>
    [HttpGet("recruiters/pending")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<RecruiterApprovalResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<IEnumerable<RecruiterApprovalResponseDto>>>> GetPendingRecruiters(
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetPendingRecruitersAsync(cancellationToken);
        return Ok(result);
    }
}
