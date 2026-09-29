using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Company;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CompanyCreateRequest> _createValidator;
    private readonly IValidator<CompanyUpdateRequest> _updateValidator;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IValidator<CompanyCreateRequest> createValidator,
        IValidator<CompanyUpdateRequest> updateValidator,
        ILogger<CompanyService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<ApiResponse<CompanyResponse>> CreateCompanyAsync(CompanyCreateRequest request, CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (recruiter == null)
        {
            _logger.LogWarning("Company creation attempted by non-recruiter user {UserId}", userId);
            throw new ForbiddenException("Only recruiters can create a company.");
        }

        if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
        {
            _logger.LogWarning("Company creation attempted by unapproved recruiter {RecruiterId}", recruiter.Id);
            throw new ForbiddenException("Recruiter account must be approved before creating a company.");
        }

        if (recruiter.CompanyId != null)
        {
            _logger.LogWarning("Recruiter {RecruiterId} already belongs to company {CompanyId}", recruiter.Id, recruiter.CompanyId);
            throw new ConflictException("You are already associated with a company. A recruiter can only belong to one company.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var company = new Company
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
            IsVerified = false,
            CreatedAt = utcNow
        };

        _context.Companies.Add(company);
        recruiter.CompanyId = company.Id;
        recruiter.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company {CompanyId} ('{CompanyName}') created by recruiter {RecruiterId}", company.Id, company.Name, recruiter.Id);

        return ApiResponse<CompanyResponse>.Ok(MapToResponse(company), "Company created successfully.");
    }

    public async Task<ApiResponse<CompanyResponse>> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company == null)
        {
            throw new EntityNotFoundException("Company", id);
        }

        return ApiResponse<CompanyResponse>.Ok(MapToResponse(company));
    }

    public async Task<ApiResponse<CompanyResponse>> UpdateCompanyAsync(Guid id, CompanyUpdateRequest request, CancellationToken cancellationToken = default)
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

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company == null)
        {
            throw new EntityNotFoundException("Company", id);
        }

        var isAdmin = _currentUserService.Role == UserRole.Admin.ToString();
        if (!isAdmin)
        {
            var recruiter = await _context.Recruiters
                .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

            if (recruiter == null || recruiter.CompanyId != id)
            {
                _logger.LogWarning("Company ownership violation: User {UserId} attempted to update company {CompanyId}", userId, id);
                throw new ForbiddenException("You do not have permission to update this company.");
            }

            if (!recruiter.IsApproved || recruiter.ApprovalStatus != RecruiterApprovalStatus.Approved)
            {
                throw new ForbiddenException("Your recruiter account is not approved.");
            }
        }

        var utcNow = _dateTimeProvider.UtcNow;
        company.Name = request.Name.Trim();
        company.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        company.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        company.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        company.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        company.UpdatedAt = utcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Company {CompanyId} updated by user {UserId}", company.Id, userId);

        return ApiResponse<CompanyResponse>.Ok(MapToResponse(company), "Company updated successfully.");
    }

    private static CompanyResponse MapToResponse(Company company)
    {
        return new CompanyResponse
        {
            Id = company.Id,
            Name = company.Name,
            Description = company.Description,
            Website = company.Website,
            Location = company.Location,
            LogoUrl = company.LogoUrl,
            IsVerified = company.IsVerified,
            CreatedAt = company.CreatedAt,
            UpdatedAt = company.UpdatedAt
        };
    }
}
