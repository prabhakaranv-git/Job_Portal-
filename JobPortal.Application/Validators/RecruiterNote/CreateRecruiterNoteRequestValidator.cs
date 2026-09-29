using FluentValidation;
using JobPortal.Application.DTOs.RecruiterNote;

namespace JobPortal.Application.Validators.RecruiterNote;

public class CreateRecruiterNoteRequestValidator : AbstractValidator<CreateRecruiterNoteRequest>
{
    public CreateRecruiterNoteRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Note content is required.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Note content cannot be whitespace only.")
            .MaximumLength(5000).WithMessage("Note content cannot exceed 5000 characters.");
    }
}
