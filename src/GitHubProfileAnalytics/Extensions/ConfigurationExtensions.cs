namespace GitHubProfileAnalytics.Extensions;

public static class ConfigurationExtensions
{
    public static string GetRequired(this IConfiguration configuration, string key)
    {
        return configuration[key]
            ?? throw new InvalidOperationException($"{key} is not configured");
    }
}
