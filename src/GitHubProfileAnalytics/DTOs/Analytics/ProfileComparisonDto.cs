namespace GitHubProfileAnalytics.DTOs.Analytics;

public class ProfileComparisonDto(IReadOnlyList<ComparisonEntryDto> profiles)
{
    public IReadOnlyList<ComparisonEntryDto> Profiles { get; init; } = profiles;
}
