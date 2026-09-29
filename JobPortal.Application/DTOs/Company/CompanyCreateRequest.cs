namespace JobPortal.Application.DTOs.Company;

public class CompanyCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? Location { get; set; }
    public string? LogoUrl { get; set; }
}
