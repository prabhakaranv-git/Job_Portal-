namespace JobPortal.Application.DTOs.Application;

public class ApplyJobRequest
{
    public Guid ResumeId { get; set; }
    public string? CoverLetter { get; set; }
}
