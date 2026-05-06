namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ActivityMetrics
{
    public int TotalEvents { get; set; }
    public int Commits { get; set; }
    public int PullRequests { get; set; }
    public int Reviews { get; set; }
    public int Issues { get; set; }
}
