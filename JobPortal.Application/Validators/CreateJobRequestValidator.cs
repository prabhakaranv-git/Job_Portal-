using FluentValidation;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Job;

namespace JobPortal.Application.Validators;

public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Job title is required.")
            .MinimumLength(2).WithMessage("Job title must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Job title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Job description is required.")
            .MinimumLength(10).WithMessage("Job description must be at least 10 characters.")
            .MaximumLength(10000).WithMessage("Job description must not exceed 10000 characters.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(200).WithMessage("Location must not exceed 200 characters.");

        RuleFor(x => x.JobType)
            .IsInEnum().WithMessage("Invalid job type.");

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum salary must be non-negative.")
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(0).WithMessage("Maximum salary must be non-negative.")
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x)
            .Must(x => !x.SalaryMin.HasValue || !x.SalaryMax.HasValue || x.SalaryMin <= x.SalaryMax)
            .WithMessage("Minimum salary cannot be greater than maximum salary.")
            .WithName("SalaryMin");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(2, 10).WithMessage("Currency must be between 2 and 10 characters.");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > dateTimeProvider.UtcNow)
            .WithMessage("Expiration date must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}
