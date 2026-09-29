using FluentValidation;
using JobPortal.Application.DTOs.Job;

namespace JobPortal.Application.Validators;

public class RecruiterJobQueryValidator : AbstractValidator<RecruiterJobQuery>
{
    private static readonly string[] AllowedSortFields = { "createdat", "title", "location", "salarymin", "salarymax", "expiresat", "status" };
    private static readonly string[] AllowedSortDirections = { "asc", "desc" };

    public RecruiterJobQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid job status.")
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
