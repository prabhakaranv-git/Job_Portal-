using FluentValidation;
using JobPortal.Application.DTOs.Application;

namespace JobPortal.Application.Validators;

public class JobSeekerApplicationQueryValidator : AbstractValidator<JobSeekerApplicationQuery>
{
    private static readonly string[] AllowedSortFields = { "appliedat", "status", "createdat" };
    private static readonly string[] AllowedSortDirections = { "asc", "desc" };

    public JobSeekerApplicationQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid application status.")
            .When(x => x.Status.HasValue);

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortFields.Contains(sortBy.ToLowerInvariant()))
            .WithMessage($"Sort field must be one of: {string.Join(", ", AllowedSortFields)}.")
            .When(x => !string.IsNullOrEmpty(x.SortBy));

        RuleFor(x => x.SortDirection)
            .Must(dir => string.IsNullOrEmpty(dir) || AllowedSortDirections.Contains(dir.ToLowerInvariant()))
            .WithMessage("Sort direction must be 'asc' or 'desc'.")
            .When(x => !string.IsNullOrEmpty(x.SortDirection));
    }
}
