namespace GitHubProfileAnalytics.DTOs.Analytics;

public class RepositoryMetrics
{
    public int TotalStars { get; set; }
    public int TotalForks { get; set; }
    public double AverageStarsPerRepo { get; set; }
    public List<LanguageStat> TopLanguages { get; set; } = [];
}
