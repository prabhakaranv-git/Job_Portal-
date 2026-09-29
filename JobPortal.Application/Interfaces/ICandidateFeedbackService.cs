using JobPortal.Application.DTOs.CandidateFeedback;
using JobPortal.Application.DTOs.Common;

namespace JobPortal.Application.Interfaces;

public interface ICandidateFeedbackService
{
    Task<ApiResponse<CandidateFeedbackResponse>> CreateFeedbackAsync(
        Guid applicationId,
        CreateCandidateFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CandidateFeedbackResponse>> GetFeedbackAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CandidateFeedbackResponse>> UpdateFeedbackAsync(
        Guid applicationId,
        UpdateCandidateFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> DeleteFeedbackAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);
}
