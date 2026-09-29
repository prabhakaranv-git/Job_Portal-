using FluentAssertions;
using FluentValidation.TestHelper;
using JobPortal.Application.DTOs.Application;
using JobPortal.Application.Validators;
using JobPortal.Domain.Enums;
using Xunit;

namespace JobPortal.Tests.Unit.Validation;

public class Phase4ValidationTests
{
    [Fact]
    public void ApplyJobRequestValidator_ValidatesResumeIdAndCoverLetter()
    {
        var validator = new ApplyJobRequestValidator();

        // Empty ResumeId
        validator.TestValidate(new ApplyJobRequest { ResumeId = Guid.Empty })
            .ShouldHaveValidationErrorFor(x => x.ResumeId);

        // Long CoverLetter
        validator.TestValidate(new ApplyJobRequest { ResumeId = Guid.NewGuid(), CoverLetter = new string('a', 5001) })
            .ShouldHaveValidationErrorFor(x => x.CoverLetter);

        // Valid
        validator.TestValidate(new ApplyJobRequest { ResumeId = Guid.NewGuid(), CoverLetter = "Passionate about backend engineering." })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateApplicationStatusRequestValidator_ValidatesStatusEnum()
    {
        var validator = new UpdateApplicationStatusRequestValidator();

        // Invalid Enum
        validator.TestValidate(new UpdateApplicationStatusRequest { Status = (ApplicationStatus)999 })
            .ShouldHaveValidationErrorFor(x => x.Status);

        // Valid
        validator.TestValidate(new UpdateApplicationStatusRequest { Status = ApplicationStatus.Shortlisted })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void JobSeekerApplicationQueryValidator_ValidatesPageAndSort()
    {
        var validator = new JobSeekerApplicationQueryValidator();

        // Page < 1
        validator.TestValidate(new JobSeekerApplicationQuery { Page = 0 })
            .ShouldHaveValidationErrorFor(x => x.Page);

        // PageSize > 50
        validator.TestValidate(new JobSeekerApplicationQuery { PageSize = 100 })
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        // Invalid SortBy
        validator.TestValidate(new JobSeekerApplicationQuery { SortBy = "invalid_column" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);

        // Valid
        validator.TestValidate(new JobSeekerApplicationQuery { Page = 1, PageSize = 20, Status = ApplicationStatus.Submitted, SortBy = "appliedAt", SortDirection = "desc" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RecruiterApplicationQueryValidator_ValidatesPageAndSort()
    {
        var validator = new RecruiterApplicationQueryValidator();

        // Page < 1
        validator.TestValidate(new RecruiterApplicationQuery { Page = -1 })
            .ShouldHaveValidationErrorFor(x => x.Page);

        // PageSize > 50
        validator.TestValidate(new RecruiterApplicationQuery { PageSize = 60 })
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        // Valid
        validator.TestValidate(new RecruiterApplicationQuery { Page = 2, PageSize = 10, Status = ApplicationStatus.UnderReview, SortBy = "status", SortDirection = "asc" })
            .ShouldNotHaveAnyValidationErrors();
    }
}
