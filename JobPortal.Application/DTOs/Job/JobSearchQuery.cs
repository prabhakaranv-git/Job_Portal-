using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Job;

public class JobSearchQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Location { get; set; }
    public JobType? JobType { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public List<Guid>? SkillIds { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
