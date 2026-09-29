using JobPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobPortal.Infrastructure.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // 1. Locate and load .env if present
        var currentDir = Directory.GetCurrentDirectory();
        LoadEnvFile(currentDir);

        var parentDir = Directory.GetParent(currentDir)?.FullName;
        if (!string.IsNullOrWhiteSpace(parentDir))
        {
            LoadEnvFile(parentDir);
        }

        // 2. Read connection string from environment
        var rawConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        var connectionString = ConnectionStringHelper.Normalize(rawConnectionString);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find a valid connection string for ApplicationDbContext. " +
                "Please ensure 'ConnectionStrings__DefaultConnection' or 'DATABASE_URL' is set in your environment or .env file.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

        var dateTimeProvider = new DateTimeProvider();
        return new ApplicationDbContext(optionsBuilder.Options, dateTimeProvider);
    }

    private static void LoadEnvFile(string directory)
    {
        try
        {
            var envPath = Path.Combine(directory, ".env");
            if (!File.Exists(envPath))
            {
                var parent = Directory.GetParent(directory)?.FullName;
                if (parent != null)
                {
                    var parentEnv = Path.Combine(parent, ".env");
                    if (File.Exists(parentEnv))
                    {
                        envPath = parentEnv;
                    }
                }
            }

            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    var equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        var key = trimmed.Substring(0, equalsIndex).Trim();
                        var value = trimmed.Substring(equalsIndex + 1).Trim();

                        if ((value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2) ||
                            (value.StartsWith('\'') && value.EndsWith('\'') && value.Length >= 2))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }

                        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        {
                            Environment.SetEnvironmentVariable(key, value);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore errors loading .env during design time
        }
    }
}
