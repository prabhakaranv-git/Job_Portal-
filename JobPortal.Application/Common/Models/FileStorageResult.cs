namespace JobPortal.Application.Common.Models;

public class FileStorageResult
{
    public string StoragePath { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
