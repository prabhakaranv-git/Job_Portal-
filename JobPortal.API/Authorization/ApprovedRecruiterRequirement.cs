using System.Security.Claims;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.API.Authorization;

public class ApprovedRecruiterRequirement : IAuthorizationRequirement
{
}

public class ApprovedRecruiterHandler : AuthorizationHandler<ApprovedRecruiterRequirement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovedRecruiterHandler> _logger;

    public ApprovedRecruiterHandler(IServiceProvider serviceProvider, ILogger<ApprovedRecruiterHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApprovedRecruiterRequirement requirement)
    {
        // 1. Admin bypasses recruiter approval requirement
        if (context.User.IsInRole(UserRole.Admin.ToString()))
        {
            context.Succeed(requirement);
            return;
        }

        // 2. Must be in Recruiter role
        if (!context.User.IsInRole(UserRole.Recruiter.ToString()))
        {
            return;
        }

        // 3. Extract UserId
        var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return;
        }

        // 4. Resolve DbContext from scoped provider to check approval status
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var recruiter = await dbContext.Recruiters
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (recruiter != null && recruiter.IsApproved)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Access denied for recruiter User {UserId}: account is not approved by administrator.", userId);
        }
    }
}
