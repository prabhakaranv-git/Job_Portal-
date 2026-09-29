using FluentValidation;
using JobPortal.Application.Interfaces;
using JobPortal.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobPortal.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IApplicationStatusTransitionService, ApplicationStatusTransitionService>();
        services.AddScoped<IResumeService, ResumeService>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();
        services.AddScoped<IRecruiterNoteService, RecruiterNoteService>();
        services.AddScoped<ICandidateFeedbackService, CandidateFeedbackService>();

        return services;
    }
}
