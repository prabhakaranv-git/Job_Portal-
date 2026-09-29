using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Job;

public class RecruiterJobQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public JobStatus? Status { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
