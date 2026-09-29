using System.Text;
using Npgsql;

namespace JobPortal.Infrastructure.Data;

public static class ConnectionStringHelper
{
    public static string Normalize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        var trimmed = connectionString.Trim();

        // Check if connection string is in URI format (e.g. postgresql:// or postgres://)
        if (trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(trimmed);
                var userInfoParts = uri.UserInfo.Split(':', 2);
                var username = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : string.Empty;
                var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                    SslMode = SslMode.Require,
                    Pooling = true,
                    Timeout = 15,
                    CommandTimeout = 30
                };

                // Parse query parameters from URI if any
                var query = uri.Query.TrimStart('?');
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].ToLowerInvariant();
                            var val = Uri.UnescapeDataString(kv[1]);

                            if (key == "sslmode" && Enum.TryParse<SslMode>(val, true, out var parsedSslMode))
                            {
                                builder.SslMode = parsedSslMode;
                            }
                        }
                    }
                }

                return builder.ConnectionString;
            }
            catch
            {
                // Fall back to returning the original string if URI parsing fails
                return trimmed;
            }
        }

        // For standard Key=Value connection strings, ensure Neon-friendly SSL & pooling defaults
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(trimmed);

            // If pointing to a Neon host (*.neon.tech), enforce SSL Require
            if (!string.IsNullOrWhiteSpace(builder.Host) && builder.Host.Contains("neon.tech", StringComparison.OrdinalIgnoreCase))
            {
                if (builder.SslMode == SslMode.Disable || builder.SslMode == SslMode.Prefer)
                {
                    builder.SslMode = SslMode.Require;
                }
            }

            if (builder.Timeout == 0)
            {
                builder.Timeout = 15;
            }

            if (builder.CommandTimeout == 0)
            {
                builder.CommandTimeout = 30;
            }

            return builder.ConnectionString;
        }
        catch
        {
            return trimmed;
        }
    }
}
