namespace JobPortal.Application.DTOs.Health;

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
    public string Service { get; set; } = "JobPortal API";
    public string Version { get; set; } = "1.0.0";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DatabaseHealthDto Database { get; set; } = new();
}

public class DatabaseHealthDto
{
    public string Status { get; set; } = "Unknown";
    public bool CanConnect { get; set; }
    public double ResponseTimeMs { get; set; }
    public string? Details { get; set; }
}
