using FluentAssertions;
using FluentValidation.TestHelper;
using JobPortal.Application.DTOs.CandidateFeedback;
using JobPortal.Application.DTOs.RecruiterNote;
using JobPortal.Application.Validators.CandidateFeedback;
using JobPortal.Application.Validators.RecruiterNote;
using JobPortal.Domain.Enums;
using Xunit;

namespace JobPortal.Tests.Unit.Validation;

public class Phase5ValidationTests
{
    private readonly CreateRecruiterNoteRequestValidator _createNoteValidator = new();
    private readonly UpdateRecruiterNoteRequestValidator _updateNoteValidator = new();
    private readonly RecruiterNoteQueryValidator _noteQueryValidator = new();
    private readonly CreateCandidateFeedbackRequestValidator _createFeedbackValidator = new();
    private readonly UpdateCandidateFeedbackRequestValidator _updateFeedbackValidator = new();

    [Fact]
    public void CreateNoteValidator_WhenContentValid_ShouldNotHaveValidationError()
    {
        var request = new CreateRecruiterNoteRequest
        {
            Content = "Candidate has strong C# and EF Core fundamentals."
        };

        var result = _createNoteValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateNoteValidator_WhenContentEmptyOrWhitespace_ShouldHaveValidationError(string? content)
    {
        var request = new CreateRecruiterNoteRequest
        {
            Content = content!
        };

        var result = _createNoteValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void CreateNoteValidator_WhenContentExceeds5000Chars_ShouldHaveValidationError()
    {
        var request = new CreateRecruiterNoteRequest
        {
            Content = new string('a', 5001)
        };

        var result = _createNoteValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void UpdateNoteValidator_WhenContentValid_ShouldNotHaveValidationError()
    {
        var request = new UpdateRecruiterNoteRequest
        {
            Content = "Updated evaluation after second round interview."
        };

        var result = _updateNoteValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NoteQueryValidator_WhenPageAndPageSizeValid_ShouldNotHaveValidationError()
    {
        var query = new RecruiterNoteQuery
        {
            Page = 1,
            PageSize = 20
        };

        var result = _noteQueryValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void NoteQueryValidator_WhenInvalidPagination_ShouldHaveValidationError(int page, int pageSize)
    {
        var query = new RecruiterNoteQuery
        {
            Page = page,
            PageSize = pageSize
        };

        var result = _noteQueryValidator.TestValidate(query);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateFeedbackValidator_WhenValid_ShouldNotHaveValidationError()
    {
        var request = new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            Strengths = "Strong architecture knowledge",
            Weaknesses = "Could improve Docker deployment experience",
            DetailedFeedback = "Great candidate overall."
        };

        var result = _createFeedbackValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void CreateFeedbackValidator_WhenRatingOutOfRange_ShouldHaveValidationError(int rating)
    {
        var request = new CreateCandidateFeedbackRequest
        {
            OverallRating = rating,
            Recommendation = FeedbackRecommendation.Hire,
            Strengths = "Solid knowledge"
        };

        var result = _createFeedbackValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.OverallRating);
    }

    [Fact]
    public void CreateFeedbackValidator_WhenRecommendationInvalid_ShouldHaveValidationError()
    {
        var request = new CreateCandidateFeedbackRequest
        {
            OverallRating = 3,
            Recommendation = (FeedbackRecommendation)99,
            Strengths = "Solid knowledge"
        };

        var result = _createFeedbackValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Recommendation);
    }

    [Fact]
    public void CreateFeedbackValidator_WhenAllTextFieldsEmpty_ShouldHaveValidationError()
    {
        var request = new CreateCandidateFeedbackRequest
        {
            OverallRating = 3,
            Recommendation = FeedbackRecommendation.Maybe,
            Strengths = "",
            Weaknesses = "   ",
            DetailedFeedback = null
        };

        var result = _createFeedbackValidator.TestValidate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateFeedbackValidator_WhenStrengthsExceeds3000Chars_ShouldHaveValidationError()
    {
        var request = new CreateCandidateFeedbackRequest
        {
            OverallRating = 4,
            Recommendation = FeedbackRecommendation.Hire,
            Strengths = new string('s', 3001)
        };

        var result = _createFeedbackValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Strengths);
    }

    [Fact]
    public void UpdateFeedbackValidator_WhenValid_ShouldNotHaveValidationError()
    {
        var request = new UpdateCandidateFeedbackRequest
        {
            OverallRating = 5,
            Recommendation = FeedbackRecommendation.StrongHire,
            DetailedFeedback = "Exceptional backend problem solving skills."
        };

        var result = _updateFeedbackValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
