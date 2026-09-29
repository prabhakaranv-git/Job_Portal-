using System.Text;
using System.Threading.RateLimiting;
using JobPortal.API.Authorization;
using JobPortal.API.Configuration;
using JobPortal.API.Services;
using JobPortal.Application.Common.Interfaces;
using JobPortal.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace JobPortal.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.Configure<ApplicationSettings>(configuration.GetSection(ApplicationSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "JobPortal API",
                Version = "v1",
                Description = "Production-grade RESTful API for Job Portal Management System.",
                Contact = new OpenApiContact
                {
                    Name = "Job Portal Engineering Team",
                    Email = "support@jobportal.local"
                }
            });

            // JWT Bearer Authorization in Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        var secretKey = string.IsNullOrWhiteSpace(jwtSettings.SecretKey)
            ? "DefaultFallbackSecretKeyForDevelopmentOnlyNeedsToBe32BytesLong!"
            : jwtSettings.SecretKey;

        var key = Encoding.UTF8.GetBytes(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        // Register authorization requirement handlers and policies
        services.AddScoped<IAuthorizationHandler, ApprovedRecruiterHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("JobSeekerOnly", policy =>
                policy.RequireRole(UserRole.JobSeeker.ToString()));

            options.AddPolicy("RecruiterOnly", policy =>
                policy.RequireRole(UserRole.Recruiter.ToString()));

            options.AddPolicy("ApprovedRecruiterOnly", policy =>
                policy.RequireRole(UserRole.Recruiter.ToString())
                      .AddRequirements(new ApprovedRecruiterRequirement()));

            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole(UserRole.Admin.ToString()));
        });

        return services;
    }

    public static IServiceCollection AddRateLimitingPolicy(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("AuthRateLimiter", opt =>
            {
                opt.PermitLimit = 30; // 30 requests per minute
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });
        });

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();
        var allowedOrigins = corsSettings?.AllowedOrigins ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("JobPortalCorsPolicy", policy =>
            {
                if (allowedOrigins.Length > 0 && !allowedOrigins.Contains("*"))
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
                else
                {
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://localhost:3000")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthCheckBuilder = services.AddHealthChecks();

        var rawConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        var connectionString = JobPortal.Infrastructure.Data.ConnectionStringHelper.Normalize(rawConnectionString);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            healthCheckBuilder.AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db", "ready" });
        }

        return services;
    }
}
