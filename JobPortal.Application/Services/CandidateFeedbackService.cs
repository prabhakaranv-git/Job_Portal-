using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.CandidateFeedback;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class CandidateFeedbackService : ICandidateFeedbackService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateCandidateFeedbackRequest> _createValidator;
    private readonly IValidator<UpdateCandidateFeedbackRequest> _updateValidator;
    private readonly ILogger<CandidateFeedbackService> _logger;

    public CandidateFeedbackService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateCandidateFeedbackRequest> createValidator,
        IValidator<UpdateCandidateFeedbackRequest> updateValidator,
        ILogger<CandidateFeedbackService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<ApiResponse<CandidateFeedbackResponse>> CreateFeedbackAsync(
        Guid applicationId,
        CreateCandidateFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        // Check if feedback already exists for this application by this recruiter
        var existingFeedback = await _context.CandidateFeedbacks
            .AnyAsync(f => f.ApplicationId == applicationId && f.RecruiterId == recruiter.Id, cancellationToken);

        if (existingFeedback)
        {
            _logger.LogWarning("Duplicate candidate feedback attempt by Recruiter {RecruiterId} for Application {ApplicationId}",
                recruiter.Id, applicationId);
            throw new ConflictException("Candidate feedback already exists for this application. Please update the existing feedback instead.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var feedback = new CandidateFeedback
        {
            ApplicationId = applicationId,
            RecruiterId = recruiter.Id,
            OverallRating = request.OverallRating,
            Strengths = string.IsNullOrWhiteSpace(request.Strengths) ? null : request.Strengths.Trim(),
            Weaknesses = string.IsNullOrWhiteSpace(request.Weaknesses) ? null : request.Weaknesses.Trim(),
            Recommendation = request.Recommendation,
            DetailedFeedback = string.IsNullOrWhiteSpace(request.DetailedFeedback) ? null : request.DetailedFeedback.Trim(),
            CreatedAt = utcNow
        };

        try
        {
            _context.CandidateFeedbacks.Add(feedback);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Unique constraint violation on CandidateFeedback (ApplicationId={ApplicationId}, RecruiterId={RecruiterId})",
                applicationId, recruiter.Id);
            throw new ConflictException("Candidate feedback already exists for this application.");
        }

        _logger.LogInformation("Candidate feedback {FeedbackId} created by Recruiter {RecruiterId} for Application {ApplicationId} (Rating: {Rating}, Recommendation: {Rec})",
            feedback.Id, recruiter.Id, applicationId, feedback.OverallRating, feedback.Recommendation);

        return ApiResponse<CandidateFeedbackResponse>.Ok(MapToResponse(feedback), "Candidate feedback created successfully.");
    }

    public async Task<ApiResponse<CandidateFeedbackResponse>> GetFeedbackAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var feedback = await _context.CandidateFeedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.ApplicationId == applicationId && f.RecruiterId == recruiter.Id, cancellationToken);

        if (feedback == null)
        {
            throw new EntityNotFoundException("CandidateFeedback for application", applicationId);
        }

        return ApiResponse<CandidateFeedbackResponse>.Ok(MapToResponse(feedback));
    }

    public async Task<ApiResponse<CandidateFeedbackResponse>> UpdateFeedbackAsync(
        Guid applicationId,
        UpdateCandidateFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var feedback = await _context.CandidateFeedbacks
            .FirstOrDefaultAsync(f => f.ApplicationId == applicationId && f.RecruiterId == recruiter.Id, cancellationToken);

        if (feedback == null)
        {
            throw new EntityNotFoundException("CandidateFeedback for application", applicationId);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        feedback.OverallRating = request.OverallRating;
        feedback.Strengths = string.IsNullOrWhiteSpace(request.Strengths) ? null : request.Strengths.Trim();
        feedback.Weaknesses = string.IsNullOrWhiteSpace(request.Weaknesses) ? null : request.Weaknesses.Trim();
        feedback.Recommendation = request.Recommendation;
        feedback.DetailedFeedback = string.IsNullOrWhiteSpace(request.DetailedFeedback) ? null : request.DetailedFeedback.Trim();
        feedback.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Candidate feedback {FeedbackId} updated by Recruiter {RecruiterId} for Application {ApplicationId} (Rating: {Rating}, Recommendation: {Rec})",
            feedback.Id, recruiter.Id, applicationId, feedback.OverallRating, feedback.Recommendation);

        return ApiResponse<CandidateFeedbackResponse>.Ok(MapToResponse(feedback), "Candidate feedback updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteFeedbackAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var feedback = await _context.CandidateFeedbacks
            .FirstOrDefaultAsync(f => f.ApplicationId == applicationId && f.RecruiterId == recruiter.Id, cancellationToken);

        if (feedback == null)
        {
            throw new EntityNotFoundException("CandidateFeedback for application", applicationId);
        }

        _context.CandidateFeedbacks.Remove(feedback);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Candidate feedback {FeedbackId} deleted by Recruiter {RecruiterId} for Application {ApplicationId}",
            feedback.Id, recruiter.Id, applicationId);

        return ApiResponse<object>.Ok(new { ApplicationId = applicationId }, "Candidate feedback deleted successfully.");
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
            throw new ForbiddenException("Recruiter account must be approved before managing candidate feedback.");
        }

        return recruiter;
    }

    private async Task VerifyApplicationAccessAsync(Guid applicationId, Guid recruiterId, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .AsNoTracking()
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application == null)
        {
            throw new EntityNotFoundException("Application", applicationId);
        }

        if (application.Job.RecruiterId != recruiterId)
        {
            _logger.LogWarning("Recruiter {RecruiterId} attempted to access feedback for application {ApplicationId} owned by {OwnerRecruiterId}",
                recruiterId, applicationId, application.Job.RecruiterId);
            throw new ForbiddenException("You do not have permission to access feedback for this application.");
        }
    }

    private static CandidateFeedbackResponse MapToResponse(CandidateFeedback feedback)
    {
        return new CandidateFeedbackResponse
        {
            Id = feedback.Id,
            ApplicationId = feedback.ApplicationId,
            RecruiterId = feedback.RecruiterId,
            OverallRating = feedback.OverallRating,
            Strengths = feedback.Strengths,
            Weaknesses = feedback.Weaknesses,
            Recommendation = feedback.Recommendation,
            DetailedFeedback = feedback.DetailedFeedback,
            CreatedAt = feedback.CreatedAt,
            UpdatedAt = feedback.UpdatedAt
        };
    }
}
