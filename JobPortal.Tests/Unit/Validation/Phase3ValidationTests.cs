using FluentAssertions;
using FluentValidation.TestHelper;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Application.DTOs.Company;
using JobPortal.Application.DTOs.Job;
using JobPortal.Application.DTOs.Skill;
using JobPortal.Application.Validators;
using JobPortal.Domain.Enums;
using Moq;
using Xunit;

namespace JobPortal.Tests.Unit.Validation;

public class Phase3ValidationTests
{
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly DateTime _fixedUtcNow = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    public Phase3ValidationTests()
    {
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _dateTimeProviderMock.Setup(d => d.UtcNow).Returns(_fixedUtcNow);
    }

    [Fact]
    public void CompanyCreateRequestValidator_ValidatesNameAndUrls()
    {
        var validator = new CompanyCreateRequestValidator();

        // Empty Name
        var invalid = new CompanyCreateRequest { Name = "" };
        validator.TestValidate(invalid).ShouldHaveValidationErrorFor(x => x.Name);

        // Invalid Website URL
        var invalidUrl = new CompanyCreateRequest { Name = "Valid Name", Website = "not-a-valid-url" };
        validator.TestValidate(invalidUrl).ShouldHaveValidationErrorFor(x => x.Website);

        // Valid
        var valid = new CompanyCreateRequest
        {
            Name = "Valid Corp",
            Website = "https://validcorp.com",
            Location = "Bangalore",
            LogoUrl = "https://validcorp.com/logo.png",
            Description = "A valid company description"
        };
        validator.TestValidate(valid).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateSkillRequestValidator_ValidatesName()
    {
        var validator = new CreateSkillRequestValidator();

        // Empty name
        validator.TestValidate(new CreateSkillRequest { Name = "" })
            .ShouldHaveValidationErrorFor(x => x.Name);

        // Short name
        validator.TestValidate(new CreateSkillRequest { Name = "A" })
            .ShouldHaveValidationErrorFor(x => x.Name);

        // Valid name
        validator.TestValidate(new CreateSkillRequest { Name = "PostgreSQL", Description = "RDBMS" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateJobRequestValidator_ValidatesTitleDescriptionSalaryAndExpiration()
    {
        var validator = new CreateJobRequestValidator(_dateTimeProviderMock.Object);

        // Missing Title
        validator.TestValidate(new CreateJobRequest { Title = "", Description = "Valid description longer than 10 chars", Location = "Chennai" })
            .ShouldHaveValidationErrorFor(x => x.Title);

        // Short Description
        validator.TestValidate(new CreateJobRequest { Title = "Valid Title", Description = "Too short", Location = "Chennai" })
            .ShouldHaveValidationErrorFor(x => x.Description);

        // Inverted Salary Range (SalaryMin > SalaryMax)
        validator.TestValidate(new CreateJobRequest
        {
            Title = "Valid Title",
            Description = "Valid description longer than 10 characters",
            Location = "Chennai",
            SalaryMin = 90000,
            SalaryMax = 50000
        }).ShouldHaveValidationErrorFor("SalaryMin");

        // Past Expiration
        validator.TestValidate(new CreateJobRequest
        {
            Title = "Valid Title",
            Description = "Valid description longer than 10 characters",
            Location = "Chennai",
            ExpiresAt = _fixedUtcNow.AddMinutes(-5)
        }).ShouldHaveValidationErrorFor(x => x.ExpiresAt);

        // Valid Job Request
        validator.TestValidate(new CreateJobRequest
        {
            Title = "Valid Job Title",
            Description = "Comprehensive description of the responsibilities and requirements.",
            Location = "Bangalore",
            JobType = JobType.FullTime,
            SalaryMin = 50000,
            SalaryMax = 90000,
            Currency = "USD",
            ExpiresAt = _fixedUtcNow.AddDays(30)
        }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void JobSearchQueryValidator_ValidatesPaginationLimits()
    {
        var validator = new JobSearchQueryValidator();

        // Page < 1
        validator.TestValidate(new JobSearchQuery { Page = 0 })
            .ShouldHaveValidationErrorFor(x => x.Page);

        // PageSize > 50
        validator.TestValidate(new JobSearchQuery { PageSize = 100 })
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        // Invalid SortBy
        validator.TestValidate(new JobSearchQuery { SortBy = "unsupported_field" })
            .ShouldHaveValidationErrorFor(x => x.SortBy);

        // Valid Query
        validator.TestValidate(new JobSearchQuery
        {
            Page = 1,
            PageSize = 25,
            Search = ".NET",
            Location = "Remote",
            JobType = JobType.Remote,
            SalaryMin = 50000,
            SalaryMax = 100000,
            SortBy = "createdAt",
            SortDirection = "desc"
        }).ShouldNotHaveAnyValidationErrors();
    }
}
