using JobPortal.Domain.Enums;

namespace JobPortal.Application.DTOs.Application;

public class UpdateApplicationStatusRequest
{
    public ApplicationStatus Status { get; set; }
}
