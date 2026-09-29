using JobPortal.API.Configuration;
using JobPortal.API.Extensions;
using JobPortal.Application.Extensions;
using JobPortal.Infrastructure.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

// Load environment variables from .env file in development if present
EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "JobPortal.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting JobPortal API application host...");

    // 2. Register Layer Services
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddRateLimitingPolicy();
    builder.Services.AddCorsPolicy(builder.Configuration);
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddCustomHealthChecks(builder.Configuration);

    var app = builder.Build();

    // 3. Configure HTTP Request Pipeline
    app.UseApiMiddleware();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "JobPortal API v1");
            c.RoutePrefix = string.Empty; // Serve Swagger at root /
        });
    }

    app.UseCors("JobPortalCorsPolicy");
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapCustomHealthChecks();

    Log.Information("JobPortal API host configured successfully. Ready to receive requests.");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "JobPortal API host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
