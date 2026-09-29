using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Resume;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using JobApplication = JobPortal.Domain.Entities.Application;

namespace JobPortal.Application.Services;

public class JobApplicationService : IJobApplicationService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileStorageService _fileStorageService;
    private readonly IApplicationStatusTransitionService _transitionService;
    private readonly IValidator<ApplyJobRequest> _applyValidator;
    private readonly IValidator<UpdateApplicationStatusRequest> _statusValidator;
    private readonly IValidator<JobSeekerApplicationQuery> _jobSeekerQueryValidator;
    private readonly IValidator<RecruiterApplicationQuery> _recruiterQueryValidator;
    private readonly ILogger<JobApplicationService> _logger;

    public JobApplicationService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IFileStorageService fileStorageService,
        IApplicationStatusTransitionService transitionService,
        IValidator<ApplyJobRequest> applyValidator,
        IValidator<UpdateApplicationStatusRequest> statusValidator,
        IValidator<JobSeekerApplicationQuery> jobSeekerQueryValidator,
        IValidator<RecruiterApplicationQuery> recruiterQueryValidator,
        ILogger<JobApplicationService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _fileStorageService = fileStorageService;
        _transitionService = transitionService;
        _applyValidator = applyValidator;
        _statusValidator = statusValidator;
        _jobSeekerQueryValidator = jobSeekerQueryValidator;
        _recruiterQueryValidator = recruiterQueryValidator;
        _logger = logger;
    }

    public async Task<ApiResponse<ApplicationResponse>> ApplyToJobAsync(Guid jobId, ApplyJobRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _applyValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);
        var utcNow = _dateTimeProvider.UtcNow;

        // 1. Verify Job exists and is published & active
        var job = await _context.Jobs
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", jobId);
        }

        if (job.Status != JobStatus.Published)
        {
            _logger.LogWarning("Application rejected: Job {JobId} is in '{Status}' status instead of Published", jobId, job.Status);
            throw new ConflictException("Applications can only be submitted for Published jobs.");
        }

        if (job.ExpiresAt.HasValue && job.ExpiresAt.Value <= utcNow)
        {
            _logger.LogWarning("Application rejected: Job {JobId} has expired on {ExpiresAt}", jobId, job.ExpiresAt);
            throw new ConflictException("This job posting has expired.");
        }

        // 2. Verify Resume exists and belongs to current Job Seeker
        var resume = await _context.Resumes
            .FirstOrDefaultAsync(r => r.Id == request.ResumeId && r.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (resume == null)
        {
            throw new EntityNotFoundException("Resume", request.ResumeId);
        }

        // 3. Application-level check for duplicate application
        var alreadyApplied = await _context.Applications
            .AnyAsync(a => a.JobId == jobId && a.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (alreadyApplied)
        {
            _logger.LogWarning("Duplicate application attempt by JobSeeker {JobSeekerId} for Job {JobId}", jobSeeker.Id, jobId);
            throw new ConflictException("You have already applied for this job.");
        }

        var application = new JobApplication
        {
            JobId = jobId,
            JobSeekerId = jobSeeker.Id,
            ResumeId = request.ResumeId,
            CoverLetter = string.IsNullOrWhiteSpace(request.CoverLetter) ? null : request.CoverLetter.Trim(),
            Status = ApplicationStatus.Submitted,
            AppliedAt = utcNow,
            CreatedAt = utcNow
        };

        try
        {
            _context.Applications.Add(application);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database unique constraint violation on JobId={JobId}, JobSeekerId={JobSeekerId}", jobId, jobSeeker.Id);
            throw new ConflictException("You have already applied for this job.");
        }

        _logger.LogInformation("Application {ApplicationId} submitted by JobSeeker {JobSeekerId} for Job {JobId}",
            application.Id, jobSeeker.Id, jobId);

        var response = new ApplicationResponse
        {
            Id = application.Id,
            JobId = job.Id,
            JobTitle = job.Title,
            CompanyId = job.CompanyId,
            CompanyName = job.Company.Name,
            JobSeekerId = jobSeeker.Id,
            ResumeId = resume.Id,
            ResumeFileName = resume.FileName,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            UpdatedAt = application.UpdatedAt
        };

        return ApiResponse<ApplicationResponse>.Ok(response, "Application submitted successfully.");
    }

    public async Task<ApiResponse<ApplicationResponse>> WithdrawApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var application = await _context.Applications
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        _transitionService.ValidateTransition(application.Status, ApplicationStatus.Withdrawn);

        var utcNow = _dateTimeProvider.UtcNow;
        application.Status = ApplicationStatus.Withdrawn;
        application.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} withdrawn by JobSeeker {JobSeekerId}", applicationId, jobSeeker.Id);

        var response = new ApplicationResponse
        {
            Id = application.Id,
            JobId = application.JobId,
            JobTitle = application.Job.Title,
            CompanyId = application.Job.CompanyId,
            CompanyName = application.Job.Company.Name,
            JobSeekerId = application.JobSeekerId,
            ResumeId = application.ResumeId,
            ResumeFileName = application.Resume.FileName,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            UpdatedAt = application.UpdatedAt
        };

        return ApiResponse<ApplicationResponse>.Ok(response, "Application withdrawn successfully.");
    }

    public async Task<ApiResponse<PagedResponse<ApplicationListItemResponse>>> GetJobSeekerApplicationsAsync(
        JobSeekerApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _jobSeekerQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var queryable = _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .Where(a => a.JobSeekerId == jobSeeker.Id);

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(a => a.Status == query.Status.Value);
        }

        var isAsc = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = query.SortBy?.ToLowerInvariant() ?? "appliedat";

        queryable = (sortBy, isAsc) switch
        {
            ("status", true) => queryable.OrderBy(a => a.Status).ThenBy(a => a.Id),
            ("status", false) => queryable.OrderByDescending(a => a.Status).ThenByDescending(a => a.Id),
            ("createdat", true) => queryable.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id),
            ("createdat", false) => queryable.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id),
            ("appliedat", true) => queryable.OrderBy(a => a.AppliedAt).ThenBy(a => a.Id),
            _ => queryable.OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.Id)
        };

        var totalCount = await queryable.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationListItemResponse
            {
                Id = a.Id,
                JobId = a.JobId,
                JobTitle = a.Job.Title,
                CompanyId = a.Job.CompanyId,
                CompanyName = a.Job.Company.Name,
                JobLocation = a.Job.Location,
                JobType = a.Job.JobType,
                JobSeekerId = a.JobSeekerId,
                ApplicantName = $"{jobSeeker.User.FirstName} {jobSeeker.User.LastName}",
                ApplicantEmail = jobSeeker.User.Email,
                ResumeId = a.ResumeId,
                ResumeFileName = a.Resume.FileName,
                CoverLetter = a.CoverLetter,
                Status = a.Status,
                AppliedAt = a.AppliedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var pagedResponse = new PagedResponse<ApplicationListItemResponse>(items, totalCount, page, pageSize);
        return ApiResponse<PagedResponse<ApplicationListItemResponse>>.Ok(pagedResponse);
    }

    public async Task<ApiResponse<ApplicationResponse>> GetJobSeekerApplicationByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var jobSeeker = await GetCurrentJobSeekerAsync(cancellationToken);

        var application = await _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.JobSeekerId == jobSeeker.Id, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        var response = new ApplicationResponse
        {
            Id = application.Id,
            JobId = application.JobId,
            JobTitle = application.Job.Title,
            CompanyId = application.Job.CompanyId,
            CompanyName = application.Job.Company.Name,
            JobSeekerId = application.JobSeekerId,
            ResumeId = application.ResumeId,
            ResumeFileName = application.Resume.FileName,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            UpdatedAt = application.UpdatedAt
        };

        return ApiResponse<ApplicationResponse>.Ok(response);
    }

    public async Task<ApiResponse<PagedResponse<ApplicationListItemResponse>>> GetRecruiterJobApplicationsAsync(
        Guid jobId,
        RecruiterApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _recruiterQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);

        // Verify Job belongs to current recruiter
        var job = await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", jobId);
        }

        if (job.RecruiterId != recruiter.Id)
        {
            _logger.LogWarning("Recruiter {RecruiterId} attempted to access applications for Job {JobId} owned by {OwnerRecruiterId}",
                recruiter.Id, jobId, job.RecruiterId);
            throw new ForbiddenException("You do not have permission to view applications for this job.");
        }

        var queryable = _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .Include(a => a.JobSeeker)
                .ThenInclude(js => js.User)
            .Where(a => a.JobId == jobId);

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(a => a.Status == query.Status.Value);
        }

        var isAsc = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = query.SortBy?.ToLowerInvariant() ?? "appliedat";

        queryable = (sortBy, isAsc) switch
        {
            ("status", true) => queryable.OrderBy(a => a.Status).ThenBy(a => a.Id),
            ("status", false) => queryable.OrderByDescending(a => a.Status).ThenByDescending(a => a.Id),
            ("createdat", true) => queryable.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id),
            ("createdat", false) => queryable.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id),
            ("appliedat", true) => queryable.OrderBy(a => a.AppliedAt).ThenBy(a => a.Id),
            _ => queryable.OrderByDescending(a => a.AppliedAt).ThenByDescending(a => a.Id)
        };

        var totalCount = await queryable.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationListItemResponse
            {
                Id = a.Id,
                JobId = a.JobId,
                JobTitle = a.Job.Title,
                CompanyId = a.Job.CompanyId,
                CompanyName = a.Job.Company.Name,
                JobLocation = a.Job.Location,
                JobType = a.Job.JobType,
                JobSeekerId = a.JobSeekerId,
                ApplicantName = $"{a.JobSeeker.User.FirstName} {a.JobSeeker.User.LastName}",
                ApplicantEmail = a.JobSeeker.User.Email,
                ResumeId = a.ResumeId,
                ResumeFileName = a.Resume.FileName,
                CoverLetter = a.CoverLetter,
                Status = a.Status,
                AppliedAt = a.AppliedAt,
                UpdatedAt = a.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var pagedResponse = new PagedResponse<ApplicationListItemResponse>(items, totalCount, page, pageSize);
        return ApiResponse<PagedResponse<ApplicationListItemResponse>>.Ok(pagedResponse);
    }

    public async Task<ApiResponse<ApplicationListItemResponse>> GetRecruiterApplicationByIdAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);

        var application = await _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .Include(a => a.JobSeeker)
                .ThenInclude(js => js.User)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        if (application.Job.RecruiterId != recruiter.Id)
        {
            _logger.LogWarning("Recruiter {RecruiterId} attempted to view application {ApplicationId} for Job {JobId} owned by {OwnerRecruiterId}",
                recruiter.Id, applicationId, application.JobId, application.Job.RecruiterId);
            throw new ForbiddenException("You do not have permission to view this application.");
        }

        var response = new ApplicationListItemResponse
        {
            Id = application.Id,
            JobId = application.JobId,
            JobTitle = application.Job.Title,
            CompanyId = application.Job.CompanyId,
            CompanyName = application.Job.Company.Name,
            JobLocation = application.Job.Location,
            JobType = application.Job.JobType,
            JobSeekerId = application.JobSeekerId,
            ApplicantName = $"{application.JobSeeker.User.FirstName} {application.JobSeeker.User.LastName}",
            ApplicantEmail = application.JobSeeker.User.Email,
            ResumeId = application.ResumeId,
            ResumeFileName = application.Resume.FileName,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            UpdatedAt = application.UpdatedAt
        };

        return ApiResponse<ApplicationListItemResponse>.Ok(response);
    }

    public async Task<ResumeDownloadDto> DownloadApplicantResumeAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);

        var application = await _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .Include(a => a.Resume)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        if (application.Job.RecruiterId != recruiter.Id)
        {
            _logger.LogWarning("Recruiter {RecruiterId} attempted to download resume for application {ApplicationId} owned by {OwnerRecruiterId}",
                recruiter.Id, applicationId, application.Job.RecruiterId);
            throw new ForbiddenException("You do not have permission to access this resume.");
        }

        var stream = await _fileStorageService.OpenReadAsync(application.Resume.StoragePath, cancellationToken);

        return new ResumeDownloadDto
        {
            FileStream = stream,
            ContentType = application.Resume.ContentType,
            DownloadFileName = application.Resume.FileName
        };
    }

    public async Task<ApiResponse<ApplicationResponse>> UpdateApplicationStatusAsync(
        Guid applicationId,
        UpdateApplicationStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);

        var application = await _context.Applications
            .Include(a => a.Job)
                .ThenInclude(j => j.Company)
            .Include(a => a.Resume)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        if (application.Job.RecruiterId != recruiter.Id)
        {
            _logger.LogWarning("Recruiter {RecruiterId} attempted to update status for application {ApplicationId} owned by {OwnerRecruiterId}",
                recruiter.Id, applicationId, application.Job.RecruiterId);
            throw new ForbiddenException("You do not have permission to update this application.");
        }

        _transitionService.ValidateTransition(application.Status, request.Status);

        var utcNow = _dateTimeProvider.UtcNow;
        application.Status = request.Status;
        application.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} status updated to '{Status}' by Recruiter {RecruiterId}",
            application.Id, request.Status, recruiter.Id);

        var response = new ApplicationResponse
        {
            Id = application.Id,
            JobId = application.JobId,
            JobTitle = application.Job.Title,
            CompanyId = application.Job.CompanyId,
            CompanyName = application.Job.Company.Name,
            JobSeekerId = application.JobSeekerId,
            ResumeId = application.ResumeId,
            ResumeFileName = application.Resume.FileName,
            CoverLetter = application.CoverLetter,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            UpdatedAt = application.UpdatedAt
        };

        return ApiResponse<ApplicationResponse>.Ok(response, "Application status updated successfully.");
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

    private async Task<Recruiter> GetCurrentApprovedRecruiterAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var recruiter = await _context.Recruiters
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (recruiter == null)
        {
            throw new ForbiddenException("Only recruiters can perform this operation.");
        }

        if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
        {
            throw new ForbiddenException("Recruiter account must be approved before managing applications.");
        }

        return recruiter;
    }
}
