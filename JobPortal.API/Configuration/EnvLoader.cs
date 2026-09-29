namespace JobPortal.API.Configuration;

public static class EnvLoader
{
    public static void Load(string? customPath = null)
    {
        try
        {
            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(customPath))
            {
                candidates.Add(customPath);
            }

            var currentDir = Directory.GetCurrentDirectory();
            candidates.Add(Path.Combine(currentDir, ".env"));

            var parentDir = Directory.GetParent(currentDir)?.FullName;
            if (!string.IsNullOrWhiteSpace(parentDir))
            {
                candidates.Add(Path.Combine(parentDir, ".env"));
            }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, ".env"));

            var envPath = candidates.FirstOrDefault(File.Exists);
            if (envPath == null)
            {
                return;
            }

            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var delimiterIndex = trimmed.IndexOf('=');
                if (delimiterIndex <= 0)
                {
                    continue;
                }

                var key = trimmed.Substring(0, delimiterIndex).Trim();
                var value = trimmed.Substring(delimiterIndex + 1).Trim();

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
        catch
        {
            // Ignore environment loading failures to avoid crashing in production environments
        }
    }
}
