using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.CandidateFeedback;

public class CandidateFeedbackResponse
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid RecruiterId { get; set; }
    public int OverallRating { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public FeedbackRecommendation Recommendation { get; set; }
    public string? DetailedFeedback { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
