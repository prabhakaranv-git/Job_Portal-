using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Company;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class JobService : IJobService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateJobRequest> _createValidator;
    private readonly IValidator<UpdateJobRequest> _updateValidator;
    private readonly IValidator<JobSearchQuery> _searchQueryValidator;
    private readonly IValidator<RecruiterJobQuery> _recruiterQueryValidator;
    private readonly ILogger<JobService> _logger;

    public JobService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateJobRequest> createValidator,
        IValidator<UpdateJobRequest> updateValidator,
        IValidator<JobSearchQuery> searchQueryValidator,
        IValidator<RecruiterJobQuery> recruiterQueryValidator,
        ILogger<JobService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _searchQueryValidator = searchQueryValidator;
        _recruiterQueryValidator = recruiterQueryValidator;
        _logger = logger;
    }

    public async Task<ApiResponse<JobResponse>> CreateJobAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var recruiter = await _context.Recruiters
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (recruiter == null)
        {
            _logger.LogWarning("Job creation attempted by non-recruiter user {UserId}", userId);
            throw new ForbiddenException("Only recruiters can post jobs.");
        }

        if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
        {
            _logger.LogWarning("Job creation attempted by unapproved recruiter {RecruiterId}", recruiter.Id);
            throw new ForbiddenException("Recruiter account must be approved before posting jobs.");
        }

        if (recruiter.CompanyId == null || recruiter.Company == null)
        {
            _logger.LogWarning("Recruiter {RecruiterId} has no associated company when attempting to post job", recruiter.Id);
            throw new ConflictException("Recruiter must be associated with a company before creating a job posting.");
        }

        var distinctSkillIds = request.SkillIds.Distinct().ToList();
        if (distinctSkillIds.Count > 0)
        {
            var existingSkillCount = await _context.Skills
                .CountAsync(s => distinctSkillIds.Contains(s.Id), cancellationToken);

            if (existingSkillCount != distinctSkillIds.Count)
            {
                throw new JobPortal.Domain.Exceptions.ValidationException("SkillIds", "One or more specified skills do not exist.");
            }
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var job = new Job
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CompanyId = recruiter.CompanyId.Value,
            RecruiterId = recruiter.Id,
            Location = request.Location.Trim(),
            JobType = request.JobType,
            Status = JobStatus.Draft,
            SalaryMin = request.SalaryMin,
            SalaryMax = request.SalaryMax,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            ExpiresAt = request.ExpiresAt,
            CreatedAt = utcNow
        };

        foreach (var skillId in distinctSkillIds)
        {
            job.Skills.Add(new JobSkill
            {
                JobId = job.Id,
                SkillId = skillId,
                IsRequired = true,
                CreatedAt = utcNow
            });
        }

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId} ('{JobTitle}') created in Draft status by Recruiter {RecruiterId}", job.Id, job.Title, recruiter.Id);

        var loadedJob = await _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstAsync(j => j.Id == job.Id, cancellationToken);

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(loadedJob), "Job created successfully in Draft status.");
    }

    public async Task<ApiResponse<JobResponse>> GetJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;

        var job = await _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null || job.Status != JobStatus.Published || (job.ExpiresAt.HasValue && job.ExpiresAt.Value <= utcNow))
        {
            throw new EntityNotFoundException("Job", id);
        }

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(job));
    }

    public async Task<ApiResponse<PagedResponse<JobListItemResponse>>> SearchJobsAsync(JobSearchQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _searchQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var utcNow = _dateTimeProvider.UtcNow;

        var queryable = _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .Where(j => j.Status == JobStatus.Published && (!j.ExpiresAt.HasValue || j.ExpiresAt.Value > utcNow));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            queryable = queryable.Where(j =>
                j.Title.ToLower().Contains(search) ||
                j.Description.ToLower().Contains(search) ||
                j.Company.Name.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            var location = query.Location.Trim().ToLower();
            queryable = queryable.Where(j => j.Location.ToLower().Contains(location));
        }

        if (query.JobType.HasValue)
        {
            queryable = queryable.Where(j => j.JobType == query.JobType.Value);
        }

        if (query.SalaryMin.HasValue)
        {
            var min = query.SalaryMin.Value;
            queryable = queryable.Where(j =>
                (j.SalaryMax != null && j.SalaryMax >= min) ||
                (j.SalaryMax == null && j.SalaryMin != null && j.SalaryMin >= min));
        }

        if (query.SalaryMax.HasValue)
        {
            var max = query.SalaryMax.Value;
            queryable = queryable.Where(j =>
                (j.SalaryMin != null && j.SalaryMin <= max) ||
                (j.SalaryMin == null && j.SalaryMax != null && j.SalaryMax <= max));
        }

        if (query.SkillIds != null && query.SkillIds.Count > 0)
        {
            queryable = queryable.Where(j => j.Skills.Any(js => query.SkillIds.Contains(js.SkillId)));
        }

        var isAsc = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = query.SortBy?.ToLowerInvariant() ?? "createdat";

        queryable = (sortBy, isAsc) switch
        {
            ("title", true) => queryable.OrderBy(j => j.Title),
            ("title", false) => queryable.OrderByDescending(j => j.Title),
            ("location", true) => queryable.OrderBy(j => j.Location),
            ("location", false) => queryable.OrderByDescending(j => j.Location),
            ("salarymin", true) => queryable.OrderBy(j => j.SalaryMin),
            ("salarymin", false) => queryable.OrderByDescending(j => j.SalaryMin),
            ("salarymax", true) => queryable.OrderBy(j => j.SalaryMax),
            ("salarymax", false) => queryable.OrderByDescending(j => j.SalaryMax),
            ("expiresat", true) => queryable.OrderBy(j => j.ExpiresAt),
            ("expiresat", false) => queryable.OrderByDescending(j => j.ExpiresAt),
            ("createdat", true) => queryable.OrderBy(j => j.CreatedAt),
            _ => queryable.OrderByDescending(j => j.CreatedAt)
        };

        var totalCount = await queryable.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobListItemResponse
            {
                Id = j.Id,
                Title = j.Title,
                CompanyId = j.CompanyId,
                CompanyName = j.Company.Name,
                CompanyLogoUrl = j.Company.LogoUrl,
                CompanyLocation = j.Company.Location,
                CompanyIsVerified = j.Company.IsVerified,
                Location = j.Location,
                JobType = j.JobType,
                Status = j.Status,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                Currency = j.Currency,
                ExpiresAt = j.ExpiresAt,
                Skills = j.Skills.Select(s => new SkillResponse
                {
                    Id = s.Skill.Id,
                    Name = s.Skill.Name,
                    Description = s.Skill.Description,
                    CreatedAt = s.Skill.CreatedAt
                }).ToList(),
                CreatedAt = j.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var pagedResponse = new PagedResponse<JobListItemResponse>(items, totalCount, page, pageSize);
        return ApiResponse<PagedResponse<JobListItemResponse>>.Ok(pagedResponse);
    }

    public async Task<ApiResponse<JobResponse>> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var job = await _context.Jobs
            .Include(j => j.Skills)
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", id);
        }

        var isAdmin = _currentUserService.Role == UserRole.Admin.ToString();
        if (!isAdmin)
        {
            var recruiter = await _context.Recruiters
                .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

            if (recruiter == null || job.RecruiterId != recruiter.Id)
            {
                _logger.LogWarning("Job ownership violation: User {UserId} attempted to update job {JobId} owned by recruiter {RecruiterId}", userId, id, job.RecruiterId);
                throw new ForbiddenException("You do not have permission to update this job.");
            }

            if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
            {
                throw new ForbiddenException("Your recruiter account is not approved.");
            }
        }

        var distinctSkillIds = request.SkillIds.Distinct().ToList();
        if (distinctSkillIds.Count > 0)
        {
            var existingSkillCount = await _context.Skills
                .CountAsync(s => distinctSkillIds.Contains(s.Id), cancellationToken);

            if (existingSkillCount != distinctSkillIds.Count)
            {
                throw new JobPortal.Domain.Exceptions.ValidationException("SkillIds", "One or more specified skills do not exist.");
            }
        }

        var utcNow = _dateTimeProvider.UtcNow;
        job.Title = request.Title.Trim();
        job.Description = request.Description.Trim();
        job.Location = request.Location.Trim();
        job.JobType = request.JobType;
        job.SalaryMin = request.SalaryMin;
        job.SalaryMax = request.SalaryMax;
        job.Currency = request.Currency.Trim().ToUpperInvariant();
        job.ExpiresAt = request.ExpiresAt;
        job.UpdatedAt = utcNow;

        // Atomic replacement of skills
        _context.JobSkills.RemoveRange(job.Skills);
        job.Skills.Clear();

        foreach (var skillId in distinctSkillIds)
        {
            job.Skills.Add(new JobSkill
            {
                JobId = job.Id,
                SkillId = skillId,
                IsRequired = true,
                CreatedAt = utcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId} updated by user {UserId}", job.Id, userId);

        var updatedJob = await _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstAsync(j => j.Id == job.Id, cancellationToken);

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(updatedJob), "Job updated successfully.");
    }

    public async Task<ApiResponse<JobResponse>> PublishJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var job = await _context.Jobs
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", id);
        }

        var isAdmin = _currentUserService.Role == UserRole.Admin.ToString();
        if (!isAdmin)
        {
            var recruiter = await _context.Recruiters
                .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

            if (recruiter == null || job.RecruiterId != recruiter.Id)
            {
                _logger.LogWarning("Job ownership violation: User {UserId} attempted to publish job {JobId} owned by recruiter {RecruiterId}", userId, id, job.RecruiterId);
                throw new ForbiddenException("You do not have permission to publish this job.");
            }

            if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
            {
                throw new ForbiddenException("Your recruiter account is not approved.");
            }
        }

        if (job.Status != JobStatus.Draft)
        {
            _logger.LogWarning("Invalid state transition: Attempted to publish job {JobId} currently in state '{Status}'", job.Id, job.Status);
            throw new ConflictException($"Cannot publish job in '{job.Status}' status. Only Draft jobs can be published.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        if (job.ExpiresAt.HasValue && job.ExpiresAt.Value <= utcNow)
        {
            throw new ConflictException("Cannot publish job: expiration date must be in the future.");
        }

        if (string.IsNullOrWhiteSpace(job.Title) || string.IsNullOrWhiteSpace(job.Description))
        {
            throw new ConflictException("Cannot publish job: title and description are required.");
        }

        job.Status = JobStatus.Published;
        job.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId} published by user {UserId}", job.Id, userId);

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(job), "Job published successfully.");
    }

    public async Task<ApiResponse<JobResponse>> CloseJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var job = await _context.Jobs
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", id);
        }

        var isAdmin = _currentUserService.Role == UserRole.Admin.ToString();
        if (!isAdmin)
        {
            var recruiter = await _context.Recruiters
                .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

            if (recruiter == null || job.RecruiterId != recruiter.Id)
            {
                _logger.LogWarning("Job ownership violation: User {UserId} attempted to close job {JobId}", userId, id);
                throw new ForbiddenException("You do not have permission to close this job.");
            }

            if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
            {
                throw new ForbiddenException("Your recruiter account is not approved.");
            }
        }

        if (job.Status != JobStatus.Published)
        {
            _logger.LogWarning("Invalid state transition: Attempted to close job {JobId} currently in state '{Status}'", job.Id, job.Status);
            throw new ConflictException($"Cannot close job in '{job.Status}' status. Only Published jobs can be closed.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        job.Status = JobStatus.Closed;
        job.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId} closed by user {UserId}", job.Id, userId);

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(job), "Job closed successfully.");
    }

    public async Task<ApiResponse<JobResponse>> ArchiveJobAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var job = await _context.Jobs
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", id);
        }

        var isAdmin = _currentUserService.Role == UserRole.Admin.ToString();
        if (!isAdmin)
        {
            var recruiter = await _context.Recruiters
                .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

            if (recruiter == null || job.RecruiterId != recruiter.Id)
            {
                _logger.LogWarning("Job ownership violation: User {UserId} attempted to archive job {JobId}", userId, id);
                throw new ForbiddenException("You do not have permission to archive this job.");
            }

            if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
            {
                throw new ForbiddenException("Your recruiter account is not approved.");
            }
        }

        if (job.Status == JobStatus.Archived)
        {
            _logger.LogWarning("Job {JobId} is already archived", job.Id);
            throw new ConflictException("Job is already archived.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        job.Status = JobStatus.Archived;
        job.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId} archived by user {UserId}", job.Id, userId);

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(job), "Job archived successfully.");
    }

    public async Task<ApiResponse<PagedResponse<JobListItemResponse>>> GetRecruiterJobsAsync(RecruiterJobQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _recruiterQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var recruiter = await _context.Recruiters
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (recruiter == null)
        {
            throw new ForbiddenException("Only recruiters can view recruiter jobs.");
        }

        var queryable = _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .Where(j => j.RecruiterId == recruiter.Id);

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(j => j.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            queryable = queryable.Where(j =>
                j.Title.ToLower().Contains(search) ||
                j.Description.ToLower().Contains(search) ||
                j.Location.ToLower().Contains(search));
        }

        var isAsc = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = query.SortBy?.ToLowerInvariant() ?? "createdat";

        queryable = (sortBy, isAsc) switch
        {
            ("title", true) => queryable.OrderBy(j => j.Title),
            ("title", false) => queryable.OrderByDescending(j => j.Title),
            ("status", true) => queryable.OrderBy(j => j.Status),
            ("status", false) => queryable.OrderByDescending(j => j.Status),
            ("location", true) => queryable.OrderBy(j => j.Location),
            ("location", false) => queryable.OrderByDescending(j => j.Location),
            ("salarymin", true) => queryable.OrderBy(j => j.SalaryMin),
            ("salarymin", false) => queryable.OrderByDescending(j => j.SalaryMin),
            ("salarymax", true) => queryable.OrderBy(j => j.SalaryMax),
            ("salarymax", false) => queryable.OrderByDescending(j => j.SalaryMax),
            ("expiresat", true) => queryable.OrderBy(j => j.ExpiresAt),
            ("expiresat", false) => queryable.OrderByDescending(j => j.ExpiresAt),
            ("createdat", true) => queryable.OrderBy(j => j.CreatedAt),
            _ => queryable.OrderByDescending(j => j.CreatedAt)
        };

        var totalCount = await queryable.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobListItemResponse
            {
                Id = j.Id,
                Title = j.Title,
                CompanyId = j.CompanyId,
                CompanyName = j.Company.Name,
                CompanyLogoUrl = j.Company.LogoUrl,
                CompanyLocation = j.Company.Location,
                CompanyIsVerified = j.Company.IsVerified,
                Location = j.Location,
                JobType = j.JobType,
                Status = j.Status,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                Currency = j.Currency,
                ExpiresAt = j.ExpiresAt,
                Skills = j.Skills.Select(s => new SkillResponse
                {
                    Id = s.Skill.Id,
                    Name = s.Skill.Name,
                    Description = s.Skill.Description,
                    CreatedAt = s.Skill.CreatedAt
                }).ToList(),
                CreatedAt = j.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var pagedResponse = new PagedResponse<JobListItemResponse>(items, totalCount, page, pageSize);
        return ApiResponse<PagedResponse<JobListItemResponse>>.Ok(pagedResponse);
    }

    public async Task<ApiResponse<JobResponse>> GetRecruiterJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedDomainException("User is not authenticated.");

        var recruiter = await _context.Recruiters
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (recruiter == null)
        {
            throw new ForbiddenException("Only recruiters can view recruiter jobs.");
        }

        var job = await _context.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.Skills)
                .ThenInclude(js => js.Skill)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new EntityNotFoundException("Job", id);
        }

        if (job.RecruiterId != recruiter.Id && _currentUserService.Role != UserRole.Admin.ToString())
        {
            throw new ForbiddenException("You do not have permission to view this job.");
        }

        return ApiResponse<JobResponse>.Ok(MapToJobResponse(job));
    }

    private static JobResponse MapToJobResponse(Job job)
    {
        return new JobResponse
        {
            Id = job.Id,
            Title = job.Title,
            Description = job.Description,
            CompanyId = job.CompanyId,
            Company = new CompanySummaryResponse
            {
                Id = job.Company.Id,
                Name = job.Company.Name,
                Location = job.Company.Location,
                LogoUrl = job.Company.LogoUrl,
                Website = job.Company.Website,
                IsVerified = job.Company.IsVerified
            },
            RecruiterId = job.RecruiterId,
            Location = job.Location,
            JobType = job.JobType,
            Status = job.Status,
            SalaryMin = job.SalaryMin,
            SalaryMax = job.SalaryMax,
            Currency = job.Currency,
            ExpiresAt = job.ExpiresAt,
            Skills = job.Skills.Select(s => new SkillResponse
            {
                Id = s.Skill.Id,
                Name = s.Skill.Name,
                Description = s.Skill.Description,
                CreatedAt = s.Skill.CreatedAt
            }).ToList(),
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt
        };
    }
}
