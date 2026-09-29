using JobPortal.Domain.Enums;

namespace JobPortal.Application.Interfaces;

public interface IApplicationStatusTransitionService
{
    bool CanTransition(ApplicationStatus from, ApplicationStatus to);
    void ValidateTransition(ApplicationStatus from, ApplicationStatus to);
}
