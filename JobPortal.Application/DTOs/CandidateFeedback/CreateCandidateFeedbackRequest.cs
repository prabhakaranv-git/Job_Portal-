using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.CandidateFeedback;

public class CreateCandidateFeedbackRequest
{
    public int OverallRating { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public FeedbackRecommendation Recommendation { get; set; }
    public string? DetailedFeedback { get; set; }
}
