using JobPortal.Application.Interfaces;
using JobPortal.Domain.Enums;
using JobPortal.Domain.Exceptions;

namespace JobPortal.Application.Services;

public class ApplicationStatusTransitionService : IApplicationStatusTransitionService
{
    private static readonly Dictionary<ApplicationStatus, HashSet<ApplicationStatus>> AllowedTransitions = new()
    {
        {
            ApplicationStatus.Submitted, new HashSet<ApplicationStatus>
            {
                ApplicationStatus.UnderReview,
                ApplicationStatus.Rejected,
                ApplicationStatus.Withdrawn
            }
        },
        {
            ApplicationStatus.UnderReview, new HashSet<ApplicationStatus>
            {
                ApplicationStatus.Shortlisted,
                ApplicationStatus.Rejected,
                ApplicationStatus.Withdrawn
            }
        },
        {
            ApplicationStatus.Shortlisted, new HashSet<ApplicationStatus>
            {
                ApplicationStatus.Interviewing,
                ApplicationStatus.Rejected,
                ApplicationStatus.Withdrawn
            }
        },
        {
            ApplicationStatus.Interviewing, new HashSet<ApplicationStatus>
            {
                ApplicationStatus.Accepted,
                ApplicationStatus.Rejected,
                ApplicationStatus.Withdrawn
            }
        },
        { ApplicationStatus.Accepted, new HashSet<ApplicationStatus>() },
        { ApplicationStatus.Rejected, new HashSet<ApplicationStatus>() },
        { ApplicationStatus.Withdrawn, new HashSet<ApplicationStatus>() }
    };

    public bool CanTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public void ValidateTransition(ApplicationStatus from, ApplicationStatus to)
    {
        if (from == to)
        {
            throw new ConflictException($"Application is already in '{from}' status.");
        }

        if (IsTerminal(from))
        {
            throw new ConflictException($"Cannot update status from terminal status '{from}'.");
        }

        if (!CanTransition(from, to))
        {
            throw new ConflictException($"Invalid status transition from '{from}' to '{to}'.");
        }
    }

    private static bool IsTerminal(ApplicationStatus status)
    {
        return status is ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn;
    }
}
