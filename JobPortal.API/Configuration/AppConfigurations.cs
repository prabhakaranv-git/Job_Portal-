namespace JobPortal.API.Configuration;

public class ApplicationSettings
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "JobPortal API";
    public string Version { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Development";
}

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
