using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Job;

namespace JobPortal.Application.Interfaces;

public interface IJobService
{
    Task<ApiResponse<JobResponse>> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResponse<JobListItemResponse>>> SearchJobsAsync(JobSearchQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> PublishJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> CloseJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> ArchiveJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<PagedResponse<JobListItemResponse>>> GetRecruiterJobsAsync(RecruiterJobQuery query, CancellationToken cancellationToken = default);
    Task<ApiResponse<JobResponse>> GetRecruiterJobByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
