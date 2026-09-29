namespace JobPortal.Application.DTOs.RecruiterNote;

public class RecruiterNoteResponse
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid RecruiterId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
