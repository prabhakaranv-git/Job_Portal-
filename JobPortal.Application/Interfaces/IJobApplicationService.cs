using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;

namespace JobPortal.Application.Interfaces;

public interface IJobApplicationService
{
    Task<ApiResponse<ApplicationResponse>> ApplyToJobAsync(Guid jobId, ApplyJobRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<ApplicationResponse>> WithdrawApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResponse<ApplicationListItemResponse>>> GetJobSeekerApplicationsAsync(JobSeekerApplicationQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse<ApplicationResponse>> GetJobSeekerApplicationByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<ApiResponse<PagedResponse<ApplicationListItemResponse>>> GetRecruiterJobApplicationsAsync(Guid jobId, RecruiterApplicationQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse<ApplicationListItemResponse>> GetRecruiterApplicationByIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<ResumeDownloadDto> DownloadApplicantResumeAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ApplicationResponse>> UpdateApplicationStatusAsync(Guid applicationId, UpdateApplicationStatusRequest request, CancellationToken cancellationToken = default);
}
