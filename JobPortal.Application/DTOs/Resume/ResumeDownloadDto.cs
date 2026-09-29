namespace JobPortal.Application.DTOs.Resume;

public class ResumeDownloadDto
{
    public Stream FileStream { get; set; } = null!;
    public string ContentType { get; set; } = string.Empty;
    public string DownloadFileName { get; set; } = string.Empty;
}
