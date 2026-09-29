using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Common;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Application.Interfaces;
using JobPortal.Domain.Entities;
using JobPortal.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPortal.Application.Services;

public class SkillService : ISkillService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<CreateSkillRequest> _validator;
    private readonly ILogger<SkillService> _logger;

    public SkillService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IValidator<CreateSkillRequest> validator,
        ILogger<SkillService> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ApiResponse<SkillResponse>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            throw new JobPortal.Domain.Exceptions.ValidationException(errors);
        }

        var normalizedName = request.Name.Trim().ToLowerInvariant();

        var exists = await _context.Skills
            .AnyAsync(s => s.Name.ToLower() == normalizedName, cancellationToken);

        if (exists)
        {
            _logger.LogWarning("Skill creation failed: skill '{SkillName}' already exists", request.Name);
            throw new ConflictException($"A skill with the name '{request.Name.Trim()}' already exists.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var skill = new Skill
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = utcNow
        };

        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Skill {SkillId} ('{SkillName}') created", skill.Id, skill.Name);

        var response = new SkillResponse
        {
            Id = skill.Id,
            Name = skill.Name,
            Description = skill.Description,
            CreatedAt = skill.CreatedAt
        };

        return ApiResponse<SkillResponse>.Ok(response, "Skill created successfully.");
    }

    public async Task<ApiResponse<IEnumerable<SkillResponse>>> GetAllSkillsAsync(CancellationToken cancellationToken = default)
    {
        var skills = await _context.Skills
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SkillResponse
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResponse<IEnumerable<SkillResponse>>.Ok(skills);
    }
}
