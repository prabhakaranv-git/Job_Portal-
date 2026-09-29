using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.Common.Models;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPortal.Application.Services;

public class ResumeService : IResumeService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileStorageService _fileStorageService;
    private readonly FileStorageOptions _storageOptions;
    private readonly ILogger<ResumeService> _logger;

    public ResumeService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IFileStorageService fileStorageService,
        IOptions<FileStorageOptions> storageOptions,
        ILogger<ResumeService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _fileStorageService = fileStorageService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<ApiResponse<ResumeResponse>> UploadResumeAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        long fileLength,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        // 1. Validate file presence & size
        if (fileStream == null || fileLength <= 0 || string.IsNullOrWhiteSpace(fileName))
        {
            throw new ValidationException("File", "A non-empty file must be uploaded.");
        }

        if (fileLength > _storageOptions.ResumeMaxSizeBytes)
        {
            var maxMb = _storageOptions.ResumeMaxSizeBytes / (1024 * 1024);
            throw new ValidationException("File", $"File size exceeds the maximum allowed limit of {maxMb} MB.");
        }

        // 2. Validate extension
        var rawExtension = Path.GetExtension(fileName);
        var extension = string.IsNullOrWhiteSpace(rawExtension) ? string.Empty : rawExtension.ToLowerInvariant();
        if (!_storageOptions.AllowedResumeExtensions.Contains(extension))
        {
            throw new ValidationException("File", $"Invalid file extension. Allowed extensions: {string.Join(", ", _storageOptions.AllowedResumeExtensions)}");
        }

        // 3. Validate content type
        var normalizedContentType = contentType.ToLowerInvariant();
        if (!_storageOptions.AllowedResumeContentTypes.Contains(normalizedContentType))
        {
            throw new ValidationException("File", $"Invalid file MIME type '{contentType}'. Allowed types: {string.Join(", ", _storageOptions.AllowedResumeContentTypes)}");
        }

        // 4. Save file to storage
        var subDirectory = jobSeeker.UserId.ToString("N");
        var storageResult = await _fileStorageService.SaveFileAsync(fileStream, fileName, normalizedContentType, subDirectory, cancellationToken);

        var utcNow = _dateTimeProvider.UtcNow;

        // 5. Default resume logic
        var hasExistingResumes = await _context.Resumes.AnyAsync(r => r.JobSeekerId == jobSeeker.Id, cancellationToken);
        var shouldBeDefault = isDefault || !hasExistingResumes;

        if (shouldBeDefault && hasExistingResumes)
        {
            var currentDefaults = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id && r.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var curDefault in currentDefaults)
            {
                curDefault.IsDefault = false;
                curDefault.UpdatedAt = utcNow;
            }
        }

        var resume = new Resume
        {
            JobSeekerId = jobSeeker.Id,
            FileName = Path.GetFileName(fileName),
            StoredFileName = storageResult.StoredFileName,
            StoragePath = storageResult.StoragePath,
            FileSizeBytes = storageResult.FileSizeBytes,
            ContentType = storageResult.ContentType,
            IsDefault = shouldBeDefault,
            CreatedAt = utcNow
        };

        _context.Resumes.Add(resume);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resume {ResumeId} uploaded for JobSeeker {JobSeekerId} (Size: {Size}, Default: {IsDefault})",
            resume.Id, jobSeeker.Id, resume.FileSizeBytes, resume.IsDefault);

        return ApiResponse<ResumeResponse>.Ok(MapToResponse(resume), "Resume uploaded successfully.");
    }

    public async Task<ApiResponse<IEnumerable<ResumeResponse>>> GetMyResumesAsync(CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var resumes = await _context.Resumes
            .AsNoTracking()
            .Where(r => r.JobSeekerId == jobSeeker.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ResumeResponse
            {
                Id = r.Id,
                FileName = r.FileName,
                ContentType = r.ContentType,
                FileSizeBytes = r.FileSizeBytes,
                IsDefault = r.IsDefault,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IEnumerable<ResumeResponse>>.Ok(resumes);
    }

    public async Task<ApiResponse<ResumeResponse>> GetResumeByIdAsync(Guid resumeId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var resume = await _context.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (resume == null)
        {
            throw new EntityNotFoundException("Resume", resumeId);
        }

        return ApiResponse<ResumeResponse>.Ok(MapToResponse(resume));
    }

    public async Task<ResumeDownloadDto> DownloadResumeAsync(Guid resumeId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var resume = await _context.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (resume == null)
        {
            throw new EntityNotFoundException("Resume", resumeId);
        }

        var stream = await _fileStorageService.OpenReadAsync(resume.StoragePath, cancellationToken);

        return new ResumeDownloadDto
        {
            FileStream = stream,
            ContentType = resume.ContentType,
            DownloadFileName = resume.FileName
        };
    }

    public async Task<ApiResponse<ResumeResponse>> SetDefaultResumeAsync(Guid resumeId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var resume = await _context.Resumes
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (resume == null)
        {
            throw new EntityNotFoundException("Resume", resumeId);
        }

        var utcNow = _dateTimeProvider.UtcNow;

        var existingDefaults = await _context.Resumes
            .Where(r => r.JobSeekerId == jobSeeker.Id && r.IsDefault && r.Id != resumeId)
            .ToListAsync(cancellationToken);

        foreach (var def in existingDefaults)
        {
            def.IsDefault = false;
            def.UpdatedAt = utcNow;
        }

        resume.IsDefault = true;
        resume.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Resume {ResumeId} set as default for JobSeeker {JobSeekerId}", resume.Id, jobSeeker.Id);

        return ApiResponse<ResumeResponse>.Ok(MapToResponse(resume), "Default resume updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteResumeAsync(Guid resumeId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var resume = await _context.Resumes
            .FirstOrDefaultAsync(r => r.Id == resumeId && r.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (resume == null)
        {
            throw new EntityNotFoundException("Resume", resumeId);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var wasDefault = resume.IsDefault;

        // If deleting the default resume, automatically promote the newest remaining resume to default
        if (wasDefault)
        {
            var nextResume = await _context.Resumes
                .Where(r => r.JobSeekerId == jobSeeker.Id && r.Id != resumeId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextResume != null)
            {
                nextResume.IsDefault = true;
                nextResume.UpdatedAt = utcNow;
            }
        }

        _context.Resumes.Remove(resume);
        await _context.SaveChangesAsync(cancellationToken);

        // Delete physical file
        await _fileStorageService.DeleteFileAsync(resume.StoragePath, cancellationToken);

        _logger.LogInformation("Resume {ResumeId} deleted for JobSeeker {JobSeekerId}", resumeId, jobSeeker.Id);

        return ApiResponse<object>.Ok(new { Id = resumeId }, "Resume deleted successfully.");
    }

    private async Task<JobSeeker> GetCurrentJobSeekerAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var jobSeeker = await _context.JobSeekers
            .Include(js => js.User)
            .FirstOrDefaultAsync(js => js.UserId == userId, cancellationToken);

        if (jobSeeker == null)
        {
            throw new ForbiddenException("Only job seekers can perform this operation.");
        }

        return jobSeeker;
    }

    private static ResumeResponse MapToResponse(Resume resume)
    {
        return new ResumeResponse
        {
            Id = resume.Id,
            FileName = resume.FileName,
            ContentType = resume.ContentType,
            FileSizeBytes = resume.FileSizeBytes,
            IsDefault = resume.IsDefault,
            CreatedAt = resume.CreatedAt,
            UpdatedAt = resume.UpdatedAt
        };
    }
}
