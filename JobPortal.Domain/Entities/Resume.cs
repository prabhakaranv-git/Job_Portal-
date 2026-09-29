using JobPortal.Domain.Common;

namespace JobPortal.Domain.Entities;

public class Resume : BaseEntity
{
    public Guid JobSeekerId { get; set; }
    public JobSeeker JobSeeker { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;

    public ICollection<Application> Applications { get; set; } = new List<Application>();
}
