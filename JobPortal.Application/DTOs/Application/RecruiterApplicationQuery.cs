using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Application;

public class RecruiterApplicationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public ApplicationStatus? Status { get; set; }
    public string? SortBy { get; set; } = "appliedAt";
    public string? SortDirection { get; set; } = "desc";
}
