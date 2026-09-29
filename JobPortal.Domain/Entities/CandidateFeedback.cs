using JobPortal.Domain.Common;
using JobPortal.Domain.Enums;

namespace JobPortal.Domain.Entities;

public class CandidateFeedback : BaseEntity
{
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public Guid RecruiterId { get; set; }
    public Recruiter Recruiter { get; set; } = null!;

    public int OverallRating { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public FeedbackRecommendation Recommendation { get; set; }
    public string? DetailedFeedback { get; set; }
}
