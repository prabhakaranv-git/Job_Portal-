using FluentValidation;
using JobPortal.Application.DTOs.Company;

namespace JobPortal.Application.Validators;

public class CompanyUpdateRequestValidator : AbstractValidator<CompanyUpdateRequest>
{
    public CompanyUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.")
            .MinimumLength(2).WithMessage("Company name must be at least 2 characters.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Company description must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Website)
            .MaximumLength(500).WithMessage("Website URL must not exceed 500 characters.")
            .Must(BeAValidUrl).WithMessage("Website must be a valid URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.Website));

        RuleFor(x => x.Location)
            .MaximumLength(200).WithMessage("Location must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.LogoUrl)
            .MaximumLength(1000).WithMessage("Logo URL must not exceed 1000 characters.")
            .Must(BeAValidUrl).WithMessage("Logo URL must be a valid URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl));
    }

    private static bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
