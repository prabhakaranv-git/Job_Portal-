using FluentAssertions;
using JobPortal.Application.Services;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;
using Xunit;

namespace JobPortal.Tests.Unit.Domain;

public class ApplicationStatusTransitionTests
{
    private readonly ApplicationStatusTransitionService _service = new();

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.Withdrawn)]
    public void CanTransition_ValidTransitions_ReturnsTrue(ApplicationStatus from, ApplicationStatus to)
    {
        var result = _service.CanTransition(from, to);
        result.Should().BeTrue();

        // ValidateTransition should not throw
        var act = () => _service.ValidateTransition(from, to);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Interviewing, ApplicationStatus.Submitted)]
    public void CanTransition_InvalidForwardOrBackwardTransitions_ReturnsFalse(ApplicationStatus from, ApplicationStatus to)
    {
        var result = _service.CanTransition(from, to);
        result.Should().BeFalse();

        var act = () => _service.ValidateTransition(from, to);
        act.Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Accepted)]
    public void TerminalStates_CannotTransitionToAnyState(ApplicationStatus terminalFrom, ApplicationStatus to)
    {
        var result = _service.CanTransition(terminalFrom, to);
        result.Should().BeFalse();

        var act = () => _service.ValidateTransition(terminalFrom, to);
        act.Should().Throw<ConflictException>();
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Shortlisted)]
    [InlineData(ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void SelfTransition_ThrowsConflictException(ApplicationStatus status)
    {
        var result = _service.CanTransition(status, status);
        result.Should().BeFalse();

        var act = () => _service.ValidateTransition(status, status);
        act.Should().Throw<ConflictException>();
    }
}
