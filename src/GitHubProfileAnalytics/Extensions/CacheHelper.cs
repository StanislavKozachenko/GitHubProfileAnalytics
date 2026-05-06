namespace GitHubProfileAnalytics.Extensions;

public static class CacheHelper
{
    public static DateTimeOffset GetThreshold(
        IConfiguration configuration,
        string key,
        int defaultHours = 1
    )
    {
        var ttlHours = configuration.GetValue<int>(key, defaultHours);
        return DateTimeOffset.UtcNow.AddHours(-ttlHours);
    }
}
