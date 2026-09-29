using FluentValidation;
using JobPortal.Application.DTOs.RecruiterNote;

namespace JobPortal.Application.Validators.RecruiterNote;

public class RecruiterNoteQueryValidator : AbstractValidator<RecruiterNoteQuery>
{
    public RecruiterNoteQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
