namespace GitHubProfileAnalytics.DTOs.Analytics;

public class RepositoryMetrics(
    int totalStars,
    int totalForks,
    double averageStarsPerRepo,
    List<LanguageStat> topLanguages
)
{
    public int TotalStars { get; init; } = totalStars;
    public int TotalForks { get; init; } = totalForks;
    public double AverageStarsPerRepo { get; init; } = averageStarsPerRepo;
    public List<LanguageStat> TopLanguages { get; init; } = topLanguages;
}
