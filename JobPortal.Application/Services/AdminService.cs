using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Admin;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        ILogger<AdminService> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<ApiResponse<RecruiterApprovalResponseDto>> ApproveRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        var recruiter = await _context.Recruiters
            .Include(r => r.User)
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.Id == recruiterId, cancellationToken);

        if (recruiter == null)
        {
            throw new EntityNotFoundException("Recruiter", recruiterId);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        recruiter.IsApproved = true;
        recruiter.ApprovalStatus = RecruiterApprovalStatus.Approved;
        recruiter.ApprovedAt = utcNow;
        recruiter.RejectedAt = null;
        recruiter.RejectionReason = null;
        recruiter.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruiter {RecruiterId} for User {UserId} was APPROVED by Admin.", recruiter.Id, recruiter.UserId);

        var responseDto = new RecruiterApprovalResponseDto
        {
            RecruiterId = recruiter.Id,
            UserId = recruiter.UserId,
            Email = recruiter.User.Email,
            FullName = $"{recruiter.User.FirstName} {recruiter.User.LastName}".Trim(),
            CompanyName = recruiter.Company?.Name,
            Position = recruiter.Position,
            ApprovalStatus = recruiter.ApprovalStatus.ToString(),
            IsApproved = recruiter.IsApproved,
            ApprovedAt = recruiter.ApprovedAt,
            RejectedAt = recruiter.RejectedAt,
            RejectionReason = recruiter.RejectionReason,
            UpdatedAt = recruiter.UpdatedAt
        };

        return ApiResponse<RecruiterApprovalResponseDto>.Ok(responseDto, "Recruiter successfully approved.");
    }

    public async Task<ApiResponse<RecruiterApprovalResponseDto>> RejectRecruiterAsync(Guid recruiterId, string? reason, CancellationToken cancellationToken = default)
    {
        var recruiter = await _context.Recruiters
            .Include(r => r.User)
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.Id == recruiterId, cancellationToken);

        if (recruiter == null)
        {
            throw new EntityNotFoundException("Recruiter", recruiterId);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        recruiter.IsApproved = false;
        recruiter.ApprovalStatus = RecruiterApprovalStatus.Rejected;
        recruiter.RejectedAt = utcNow;
        recruiter.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Application does not meet the platform requirements." : reason.Trim();
        recruiter.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruiter {RecruiterId} for User {UserId} was REJECTED by Admin. Reason: {Reason}", recruiter.Id, recruiter.UserId, recruiter.RejectionReason);

        var responseDto = new RecruiterApprovalResponseDto
        {
            RecruiterId = recruiter.Id,
            UserId = recruiter.UserId,
            Email = recruiter.User.Email,
            FullName = $"{recruiter.User.FirstName} {recruiter.User.LastName}".Trim(),
            CompanyName = recruiter.Company?.Name,
            Position = recruiter.Position,
            ApprovalStatus = recruiter.ApprovalStatus.ToString(),
            IsApproved = recruiter.IsApproved,
            ApprovedAt = recruiter.ApprovedAt,
            RejectedAt = recruiter.RejectedAt,
            RejectionReason = recruiter.RejectionReason,
            UpdatedAt = recruiter.UpdatedAt
        };

        return ApiResponse<RecruiterApprovalResponseDto>.Ok(responseDto, "Recruiter application rejected.");
    }

    public async Task<ApiResponse<IEnumerable<RecruiterApprovalResponseDto>>> GetPendingRecruitersAsync(CancellationToken cancellationToken = default)
    {
        var pendingRecruiters = await _context.Recruiters
            .Include(r => r.User)
            .Include(r => r.Company)
            .Where(r => r.ApprovalStatus == RecruiterApprovalStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RecruiterApprovalResponseDto
            {
                RecruiterId = r.Id,
                UserId = r.UserId,
                Email = r.User.Email,
                FullName = $"{r.User.FirstName} {r.User.LastName}".Trim(),
                CompanyName = r.Company != null ? r.Company.Name : null,
                Position = r.Position,
                ApprovalStatus = r.ApprovalStatus.ToString(),
                IsApproved = r.IsApproved,
                ApprovedAt = r.ApprovedAt,
                RejectedAt = r.RejectedAt,
                RejectionReason = r.RejectionReason,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IEnumerable<RecruiterApprovalResponseDto>>.Ok(pendingRecruiters);
    }
}
