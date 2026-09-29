using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.RecruiterNote;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class RecruiterNoteService : IRecruiterNoteService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateRecruiterNoteRequest> _createValidator;
    private readonly IValidator<UpdateRecruiterNoteRequest> _updateValidator;
    private readonly IValidator<RecruiterNoteQuery> _queryValidator;
    private readonly ILogger<RecruiterNoteService> _logger;

    public RecruiterNoteService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateRecruiterNoteRequest> createValidator,
        IValidator<UpdateRecruiterNoteRequest> updateValidator,
        IValidator<RecruiterNoteQuery> queryValidator,
        ILogger<RecruiterNoteService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _queryValidator = queryValidator;
        _logger = logger;
    }

    public async Task<ApiResponse<RecruiterNoteResponse>> CreateNoteAsync(
        Guid applicationId,
        CreateRecruiterNoteRequest request,
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

        var utcNow = _dateTimeProvider.UtcNow;
        var note = new RecruiterNote
        {
            ApplicationId = applicationId,
            RecruiterId = recruiter.Id,
            Content = request.Content.Trim(),
            CreatedAt = utcNow
        };

        _context.RecruiterNotes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruiter note {NoteId} created by Recruiter {RecruiterId} for Application {ApplicationId}",
            note.Id, recruiter.Id, applicationId);

        return ApiResponse<RecruiterNoteResponse>.Ok(MapToResponse(note), "Recruiter note created successfully.");
    }

    public async Task<ApiResponse<PagedResponse<RecruiterNoteResponse>>> GetNotesAsync(
        Guid applicationId,
        RecruiterNoteQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _queryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var queryable = _context.RecruiterNotes
            .AsNoTracking()
            .Where(n => n.ApplicationId == applicationId && n.RecruiterId == recruiter.Id)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id);

        var totalCount = await queryable.CountAsync(cancellationToken);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

        var items = await queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new RecruiterNoteResponse
            {
                Id = n.Id,
                ApplicationId = n.ApplicationId,
                RecruiterId = n.RecruiterId,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var pagedResponse = new PagedResponse<RecruiterNoteResponse>(items, totalCount, page, pageSize);
        return ApiResponse<PagedResponse<RecruiterNoteResponse>>.Ok(pagedResponse);
    }

    public async Task<ApiResponse<RecruiterNoteResponse>> GetNoteByIdAsync(
        Guid applicationId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var note = await _context.RecruiterNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId && n.ApplicationId == applicationId && n.RecruiterId == recruiter.Id, cancellationToken);

        if (note == null)
        {
            throw new EntityNotFoundException("RecruiterNote", noteId);
        }

        return ApiResponse<RecruiterNoteResponse>.Ok(MapToResponse(note));
    }

    public async Task<ApiResponse<RecruiterNoteResponse>> UpdateNoteAsync(
        Guid applicationId,
        Guid noteId,
        UpdateRecruiterNoteRequest request,
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

        var note = await _context.RecruiterNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.ApplicationId == applicationId && n.RecruiterId == recruiter.Id, cancellationToken);

        if (note == null)
        {
            throw new EntityNotFoundException("RecruiterNote", noteId);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        note.Content = request.Content.Trim();
        note.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruiter note {NoteId} updated by Recruiter {RecruiterId} for Application {ApplicationId}",
            note.Id, recruiter.Id, applicationId);

        return ApiResponse<RecruiterNoteResponse>.Ok(MapToResponse(note), "Recruiter note updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteNoteAsync(
        Guid applicationId,
        Guid noteId,
        CancellationToken cancellationToken = default)
    {
        var recruiter = await GetCurrentApprovedRecruiterAsync(cancellationToken);
        await VerifyApplicationAccessAsync(applicationId, recruiter.Id, cancellationToken);

        var note = await _context.RecruiterNotes
            .FirstOrDefaultAsync(n => n.Id == noteId && n.ApplicationId == applicationId && n.RecruiterId == recruiter.Id, cancellationToken);

        if (note == null)
        {
            throw new EntityNotFoundException("RecruiterNote", noteId);
        }

        _context.RecruiterNotes.Remove(note);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recruiter note {NoteId} deleted by Recruiter {RecruiterId} for Application {ApplicationId}",
            noteId, recruiter.Id, applicationId);

        return ApiResponse<object>.Ok(new { Id = noteId }, "Recruiter note deleted successfully.");
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
            throw new ForbiddenException("Recruiter account must be approved before managing notes.");
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
            _logger.LogWarning("Recruiter {RecruiterId} attempted to access notes for application {ApplicationId} owned by {OwnerRecruiterId}",
                recruiterId, applicationId, application.Job.RecruiterId);
            throw new ForbiddenException("You do not have permission to access notes for this application.");
        }
    }

    private static RecruiterNoteResponse MapToResponse(RecruiterNote note)
    {
        return new RecruiterNoteResponse
        {
            Id = note.Id,
            ApplicationId = note.ApplicationId,
            RecruiterId = note.RecruiterId,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };
    }
}
