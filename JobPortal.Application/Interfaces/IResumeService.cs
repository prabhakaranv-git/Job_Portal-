using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;

namespace JobPortal.Application.Interfaces;

public interface IResumeService
{
    Task<ApiResponse<ResumeResponse>> UploadResumeAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        bool isDefault,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IEnumerable<ResumeResponse>>> GetMyResumesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<ResumeResponse>> GetResumeByIdAsync(Guid resumeId, CancellationToken cancellationToken = default);
    Task<ResumeDownloadDto> DownloadResumeAsync(Guid resumeId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ResumeResponse>> SetDefaultResumeAsync(Guid resumeId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> DeleteResumeAsync(Guid resumeId, CancellationToken cancellationToken = default);
}
