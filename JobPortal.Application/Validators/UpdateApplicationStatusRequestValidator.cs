using FluentValidation;
using JobPortal.Application.DTOs.Application;

namespace JobPortal.Application.Validators;

public class UpdateApplicationStatusRequestValidator : AbstractValidator<UpdateApplicationStatusRequest>
{
    public UpdateApplicationStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid application status.");
    }
}
