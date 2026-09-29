using FluentValidation;
using JobPortal.Application.DTOs.Job;

namespace JobPortal.Application.Validators;

public class JobSearchQueryValidator : AbstractValidator<JobSearchQuery>
{
    private static readonly string[] AllowedSortFields = { "createdat", "title", "location", "salarymin", "salarymax", "expiresat" };
    private static readonly string[] AllowedSortDirections = { "asc", "desc" };

    public JobSearchQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.JobType)
            .IsInEnum().WithMessage("Invalid job type.")
            .When(x => x.JobType.HasValue);

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("Salary minimum must be non-negative.")
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(0).WithMessage("Salary maximum must be non-negative.")
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x)
            .Must(x => !x.SalaryMin.HasValue || !x.SalaryMax.HasValue || x.SalaryMin <= x.SalaryMax)
            .WithMessage("Minimum salary filter cannot be greater than maximum salary filter.")
            .WithName("SalaryMin");

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
