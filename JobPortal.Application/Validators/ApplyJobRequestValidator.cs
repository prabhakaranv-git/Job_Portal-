using FluentValidation;
using JobPortal.Application.DTOs.Application;

namespace JobPortal.Application.Validators;

public class ApplyJobRequestValidator : AbstractValidator<ApplyJobRequest>
{
    public ApplyJobRequestValidator()
    {
        RuleFor(x => x.ResumeId)
            .NotEmpty().WithMessage("A valid resume must be selected for the application.");

        RuleFor(x => x.CoverLetter)
            .MaximumLength(5000).WithMessage("Cover letter must not exceed 5000 characters.")
            .When(x => !string.IsNullOrEmpty(x.CoverLetter));
    }
}
