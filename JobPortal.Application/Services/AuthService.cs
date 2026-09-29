using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Auth;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<RefreshTokenRequestDto> _refreshTokenValidator;
    private readonly IValidator<RevokeTokenRequestDto> _revokeTokenValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        ICurrentUserService currentUserService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<RefreshTokenRequestDto> refreshTokenValidator,
        IValidator<RevokeTokenRequestDto> revokeTokenValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshTokenValidator = refreshTokenValidator;
        _revokeTokenValidator = revokeTokenValidator;
        _changePasswordValidator = changePasswordValidator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Check for duplicate email (case-insensitive)
        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            _logger.LogInformation("Registration attempt failed: email already exists for {Email}", normalizedEmail);
            throw new ConflictException("An account with this email already exists.");
        }

        // 2. Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var utcNow = _dateTimeProvider.UtcNow;

        // 3. Create User entity
        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = request.Role,
            IsActive = true,
            CreatedAt = utcNow
        };

        _context.Users.Add(user);

        // 4. Create associated role profile atomically
        if (request.Role == UserRole.JobSeeker)
        {
            var jobSeeker = new JobSeeker
            {
                UserId = user.Id,
                CreatedAt = utcNow
            };
            _context.JobSeekers.Add(jobSeeker);
        }
        else if (request.Role == UserRole.Recruiter)
        {
            Guid? companyId = null;
            if (!string.IsNullOrWhiteSpace(request.CompanyName))
            {
                var normalizedCompanyName = request.CompanyName.Trim().ToLowerInvariant();
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == normalizedCompanyName, cancellationToken);

                if (company == null)
                {
                    company = new Company
                    {
                        Name = request.CompanyName.Trim(),
                        CreatedAt = utcNow
                    };
                    _context.Companies.Add(company);
                }
                companyId = company.Id;
            }

            var recruiter = new Recruiter
            {
                UserId = user.Id,
                CompanyId = companyId,
                Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim(),
                IsApproved = false,
                ApprovalStatus = RecruiterApprovalStatus.Pending,
                CreatedAt = utcNow
            };
            _context.Recruiters.Add(recruiter);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 5. Generate initial token pair
        var (accessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(user);
        var (refreshEntity, rawRefreshToken) = _jwtTokenGenerator.GenerateRefreshToken(user.Id);

        _context.RefreshTokens.Add(refreshEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var expirationMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 15;

        var responseDto = new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString(),
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresInSeconds = expirationMinutes * 60,
            AccessTokenExpiresAt = accessExpiresAt,
            IsApproved = request.Role == UserRole.Recruiter ? false : null
        };

        _logger.LogInformation("User {UserId} registered successfully with role {Role}", user.Id, user.Role);
        return ApiResponse<AuthResponseDto>.Ok(responseDto, "Registration successful.");
    }

    public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Find user by email
        var user = await _context.Users
            .Include(u => u.RecruiterProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        // 2. Generic authentication failure to prevent user enumeration
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Authentication failed for email: {Email}", normalizedEmail);
            throw new UnauthorizedDomainException("Invalid email or password.");
        }

        // 3. Check account activation
        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt rejected for inactive user {UserId}", user.Id);
            throw new UnauthorizedDomainException("This account is inactive. Please contact support.");
        }

        // 4. Update login timestamp
        user.LastLoginAt = _dateTimeProvider.UtcNow;

        // 5. Generate tokens
        var (accessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(user);
        var (refreshEntity, rawRefreshToken) = _jwtTokenGenerator.GenerateRefreshToken(user.Id);

        _context.RefreshTokens.Add(refreshEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var expirationMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 15;

        var responseDto = new AuthResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString(),
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresInSeconds = expirationMinutes * 60,
            AccessTokenExpiresAt = accessExpiresAt,
            IsApproved = user.Role == UserRole.Recruiter ? user.RecruiterProfile?.IsApproved : null
        };

        _logger.LogInformation("User {UserId} logged in successfully.", user.Id);
        return ApiResponse<AuthResponseDto>.Ok(responseDto, "Login successful.");
    }

    public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _refreshTokenValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var hashedToken = _jwtTokenGenerator.HashToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .ThenInclude(u => u.RecruiterProfile)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, cancellationToken);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found in database.");
            throw new UnauthorizedDomainException("Invalid refresh token.");
        }

        // Token Reuse Detection: If token is already revoked, assume theft/compromise
        if (storedToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token reuse detected for UserId {UserId}", storedToken.UserId);

            // Revoke all active refresh tokens for this user as a security measure
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == storedToken.UserId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);

            var now = _dateTimeProvider.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
                token.ReplacedByToken = "REVOKED_DUE_TO_REUSE_ATTEMPT";
            }

            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedDomainException("Refresh token has been revoked. Security compromise detected; please log in again.");
        }

        if (storedToken.IsExpired)
        {
            _logger.LogInformation("Expired refresh token presented for user {UserId}", storedToken.UserId);
            throw new UnauthorizedDomainException("Refresh token has expired. Please log in again.");
        }

        if (!storedToken.User.IsActive)
        {
            _logger.LogWarning("Refresh token attempt for inactive user {UserId}", storedToken.UserId);
            throw new UnauthorizedDomainException("User account is inactive.");
        }

        var utcNow = _dateTimeProvider.UtcNow;

        // Atomically rotate refresh token
        var (newRefreshEntity, newRawRefreshToken) = _jwtTokenGenerator.GenerateRefreshToken(storedToken.UserId);
        storedToken.RevokedAt = utcNow;
        storedToken.ReplacedByToken = newRefreshEntity.Token;

        _context.RefreshTokens.Add(newRefreshEntity);
        await _context.SaveChangesAsync(cancellationToken);

        var (newAccessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(storedToken.User);
        var expirationMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 15;

        var responseDto = new AuthResponseDto
        {
            UserId = storedToken.User.Id,
            Email = storedToken.User.Email,
            FirstName = storedToken.User.FirstName,
            LastName = storedToken.User.LastName,
            Role = storedToken.User.Role.ToString(),
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresInSeconds = expirationMinutes * 60,
            AccessTokenExpiresAt = accessExpiresAt,
            IsApproved = storedToken.User.Role == UserRole.Recruiter ? storedToken.User.RecruiterProfile?.IsApproved : null
        };

        _logger.LogInformation("Refresh token rotated successfully for user {UserId}", storedToken.UserId);
        return ApiResponse<AuthResponseDto>.Ok(responseDto, "Token refreshed successfully.");
    }

    public async Task<ApiResponse> RevokeTokenAsync(RevokeTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _revokeTokenValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var hashedToken = _jwtTokenGenerator.HashToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken, cancellationToken);

        if (storedToken != null && !storedToken.IsRevoked)
        {
            // If caller is authenticated, ensure the token belongs to them
            if (_currentUserService.IsAuthenticated && _currentUserService.UserId.HasValue && storedToken.UserId != _currentUserService.UserId.Value)
            {
                _logger.LogWarning("User {UserId} attempted to revoke token belonging to User {OwnerId}", _currentUserService.UserId, storedToken.UserId);
                throw new ForbiddenException("You cannot revoke a refresh token that does not belong to your account.");
            }

            storedToken.RevokedAt = _dateTimeProvider.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);
        }

        return ApiResponse.Ok("Refresh token successfully revoked.");
    }

    public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedDomainException("Authentication required to change password.");
        }

        var user = await _context.Users.FindAsync(new object[] { _currentUserService.UserId.Value }, cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", _currentUserService.UserId.Value);
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Failed password change attempt for user {UserId}: incorrect current password.", user.Id);
            throw new JobPortal.Domain.Exceptions.ValidationException("CurrentPassword", "Current password is incorrect.");
        }

        var utcNow = _dateTimeProvider.UtcNow;

        // 1. Update password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = utcNow;

        // 2. Revoke all active refresh tokens after password change
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = utcNow;
            token.ReplacedByToken = "PASSWORD_CHANGED";
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password successfully changed and all sessions revoked for user {UserId}", user.Id);
        return ApiResponse.Ok("Password changed successfully. All active sessions have been invalidated. Please log in with your new password.");
    }

    public async Task<ApiResponse<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedDomainException("Authentication required.");
        }

        var user = await _context.Users
            .Include(u => u.JobSeekerProfile)
            .Include(u => u.RecruiterProfile)
            .ThenInclude(r => r!.Company)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", _currentUserService.UserId.Value);
        }

        var profile = new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };

        if (user.JobSeekerProfile != null)
        {
            profile.JobSeeker = new JobSeekerProfileSummaryDto
            {
                Id = user.JobSeekerProfile.Id,
                Headline = user.JobSeekerProfile.Headline,
                Summary = user.JobSeekerProfile.Summary,
                ExperienceYears = user.JobSeekerProfile.ExperienceYears,
                CurrentLocation = user.JobSeekerProfile.CurrentLocation
            };
        }

        if (user.RecruiterProfile != null)
        {
            profile.Recruiter = new RecruiterProfileSummaryDto
            {
                Id = user.RecruiterProfile.Id,
                CompanyId = user.RecruiterProfile.CompanyId,
                CompanyName = user.RecruiterProfile.Company?.Name,
                Position = user.RecruiterProfile.Position,
                IsApproved = user.RecruiterProfile.IsApproved,
                ApprovalStatus = user.RecruiterProfile.ApprovalStatus.ToString()
            };
        }

        return ApiResponse<UserProfileDto>.Ok(profile);
    }
}
