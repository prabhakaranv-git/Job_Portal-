using JobPortal.Application.DTOs.Admin;
using JobPortal.Application.DTOs.Common;

namespace JobPortal.Application.Interfaces;

public interface IAdminService
{
    Task<ApiResponse<RecruiterApprovalResponseDto>> ApproveRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default);
    Task<ApiResponse<RecruiterApprovalResponseDto>> RejectRecruiterAsync(Guid recruiterId, string? reason, CancellationToken cancellationToken = default);
    Task<ApiResponse<IEnumerable<RecruiterApprovalResponseDto>>> GetPendingRecruitersAsync(CancellationToken cancellationToken = default);
}
