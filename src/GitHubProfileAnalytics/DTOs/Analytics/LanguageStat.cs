namespace GitHubProfileAnalytics.DTOs.Analytics;

public class LanguageStat(string name, double percent)
{
    public string Name { get; init; } = name;
    public double Percent { get; init; } = percent;
}
