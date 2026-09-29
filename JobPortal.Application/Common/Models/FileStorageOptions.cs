namespace JobPortal.Application.Common.Models;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string ResumeStoragePath { get; set; } = "uploads/resumes";
    public long ResumeMaxSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
    public string[] AllowedResumeExtensions { get; set; } = { ".pdf", ".doc", ".docx" };
    public string[] AllowedResumeContentTypes { get; set; } =
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };
}
