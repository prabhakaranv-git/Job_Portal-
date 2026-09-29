using FluentValidation;
using JobPortal.Application.DTOs.CandidateFeedback;

namespace JobPortal.Application.Validators.CandidateFeedback;

public class CreateCandidateFeedbackRequestValidator : AbstractValidator<CreateCandidateFeedbackRequest>
{
    public CreateCandidateFeedbackRequestValidator()
    {
        RuleFor(x => x.OverallRating)
            .InclusiveBetween(1, 5).WithMessage("Overall rating must be between 1 (Very Poor) and 5 (Exceptional).");

        RuleFor(x => x.Recommendation)
            .IsInEnum().WithMessage("Recommendation must be a valid option (StrongHire, Hire, Maybe, NoHire, StrongNoHire).");

        RuleFor(x => x.Strengths)
            .MaximumLength(3000).WithMessage("Strengths summary cannot exceed 3000 characters.");

        RuleFor(x => x.Weaknesses)
            .MaximumLength(3000).WithMessage("Weaknesses summary cannot exceed 3000 characters.");

        RuleFor(x => x.DetailedFeedback)
            .MaximumLength(5000).WithMessage("Detailed feedback cannot exceed 5000 characters.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Strengths) ||
                       !string.IsNullOrWhiteSpace(x.Weaknesses) ||
                       !string.IsNullOrWhiteSpace(x.DetailedFeedback))
            .WithMessage("At least one evaluation text field (Strengths, Weaknesses, or DetailedFeedback) must be provided.");
    }
}
