namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ProfileComparisonDto(IReadOnlyList<ComparisonEntryDto> profiles)
{
    public IReadOnlyList<ComparisonEntryDto> Profiles { get; init; } = profiles;
    public string? Winner { get; init; } =
        profiles.Count == 2 && profiles[0].Score != profiles[1].Score
            ? profiles[0].Score > profiles[1].Score
                ? profiles[0].Username
                : profiles[1].Username
            : null;
}
