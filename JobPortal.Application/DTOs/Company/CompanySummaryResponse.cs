namespace JobPortal.Application.DTOs.Company;

public class CompanySummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? LogoUrl { get; set; }
    public string? Website { get; set; }
    public bool IsVerified { get; set; }
}
