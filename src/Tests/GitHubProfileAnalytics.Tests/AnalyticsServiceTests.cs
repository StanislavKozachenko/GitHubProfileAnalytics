using System.Runtime.CompilerServices;
using GitHubProfileAnalytics.DTOs.GitHub;
using GitHubProfileAnalytics.Services.Analytics;
using GitHubProfileAnalytics.Services.GitHub;
using NSubstitute;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public sealed class AnalyticsServiceTests
{
    private static AnalyticsService CreateSut(
        GitHubProfileDto profile,
        List<Repository> repos,
        List<Activity> events
    )
    {
        var gitHubService = Substitute.For<IGitHubService>();
        gitHubService.GetProfileAsync(Arg.Any<string>()).Returns(profile);

        var gitHubClient = Substitute.For<IGitHubClient>();
        gitHubClient
            .Repository.GetAllForUser(Arg.Any<string>())
            .Returns((IReadOnlyList<Repository>)repos);
        gitHubClient
            .Activity.Events.GetAllUserPerformed(Arg.Any<string>())
            .Returns((IReadOnlyList<Activity>)events);

        return new AnalyticsService(gitHubService, gitHubClient);
    }

    private static Repository CreateRepo(int stars = 0, int forks = 0, string? language = null)
    {
        var repo = (Repository)RuntimeHelpers.GetUninitializedObject(typeof(Repository));
        Set(repo, "StargazersCount", stars);
        Set(repo, "ForksCount", forks);
        Set(repo, "Language", language);
        return repo;
    }

    private static Activity CreateEvent(string type, DateTimeOffset? createdAt = null)
    {
        var activity = (Activity)RuntimeHelpers.GetUninitializedObject(typeof(Activity));
        Set(activity, "Type", type);
        Set(activity, "CreatedAt", createdAt ?? DateTimeOffset.UtcNow);
        return activity;
    }

    private static Activity CreatePushEvent(int commitCount, DateTimeOffset? createdAt = null)
    {
        var commits = Enumerable
            .Range(0, commitCount)
            .Select(_ => (Commit)RuntimeHelpers.GetUninitializedObject(typeof(Commit)))
            .ToList();

        var payload = (PushEventPayload)
            RuntimeHelpers.GetUninitializedObject(typeof(PushEventPayload));
        Set(payload, "Commits", (IReadOnlyList<Commit>)commits);

        var activity = (Activity)RuntimeHelpers.GetUninitializedObject(typeof(Activity));
        Set(activity, "Type", "PushEvent");
        Set(activity, "Payload", payload);
        Set(activity, "CreatedAt", createdAt ?? DateTimeOffset.UtcNow);
        return activity;
    }

    private static void Set(object obj, string name, object? value) =>
        obj.GetType().GetProperty(name)!.SetValue(obj, value);

    private static GitHubProfileDto CreateProfile(
        int followers = 0,
        int following = 0,
        int publicRepos = 0,
        DateTimeOffset? createdAt = null
    ) =>
        new()
        {
            Followers = followers,
            Following = following,
            PublicRepos = publicRepos,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-365).AddHours(-1),
        };

    [Fact]
    public async Task CalculatesAccountAgeDaysCorrectly()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-100).AddHours(-1);
        var sut = CreateSut(CreateProfile(createdAt: createdAt), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(100, result.Profile.AccountAgeDays);
    }

    [Theory]
    [InlineData(10, 5, 2.0)]
    [InlineData(0, 5, 0.0)]
    [InlineData(7, 3, 2.33)]
    public async Task CalculatesFollowerRatioCorrectly(
        int followers,
        int following,
        double expected
    )
    {
        var sut = CreateSut(CreateProfile(followers: followers, following: following), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(expected, result.Profile.FollowerRatio);
    }

    [Fact]
    public async Task ReturnsFollowersAsFollowerRatioWhenFollowingIsZero()
    {
        var sut = CreateSut(CreateProfile(followers: 15, following: 0), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(15.0, result.Profile.FollowerRatio);
    }

    [Theory]
    [InlineData(365, 365, 365.0)]
    [InlineData(10, 365, 10.0)]
    [InlineData(5, 730, 2.5)]
    public async Task CalculatesReposPerYearCorrectly(int publicRepos, int ageDays, double expected)
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-ageDays).AddHours(-1);
        var sut = CreateSut(CreateProfile(publicRepos: publicRepos, createdAt: createdAt), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(expected, result.Profile.ReposPerYear);
    }

    [Fact]
    public async Task ReturnsZeroReposPerYearWhenAccountAgeIsZero()
    {
        var sut = CreateSut(CreateProfile(publicRepos: 10, createdAt: DateTimeOffset.UtcNow), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(0, result.Profile.ReposPerYear);
    }

    [Fact]
    public async Task CalculatesTotalStarsCorrectly()
    {
        var repos = new List<Repository>
        {
            CreateRepo(stars: 3),
            CreateRepo(stars: 5),
            CreateRepo(stars: 2),
        };
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(10, result.Repositories.TotalStars);
    }

    [Fact]
    public async Task CalculatesTotalForksCorrectly()
    {
        var repos = new List<Repository>
        {
            CreateRepo(forks: 1),
            CreateRepo(forks: 4),
            CreateRepo(forks: 2),
        };
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(7, result.Repositories.TotalForks);
    }

    [Fact]
    public async Task CalculatesAverageStarsPerRepoCorrectly()
    {
        var repos = new List<Repository>
        {
            CreateRepo(stars: 3),
            CreateRepo(stars: 5),
            CreateRepo(stars: 4),
        };
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(4.0, result.Repositories.AverageStarsPerRepo);
    }

    [Fact]
    public async Task ReturnsZeroAverageStarsWhenNoRepos()
    {
        var sut = CreateSut(CreateProfile(), [], []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(0, result.Repositories.AverageStarsPerRepo);
    }

    [Fact]
    public async Task CalculatesTopLanguagePercentagesCorrectly()
    {
        var repos = new List<Repository>
        {
            CreateRepo(language: "C#"),
            CreateRepo(language: "C#"),
            CreateRepo(language: "C#"),
            CreateRepo(language: "TypeScript"),
        };
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        var csharp = result.Repositories.TopLanguages.Single(l => l.Name == "C#");
        var ts = result.Repositories.TopLanguages.Single(l => l.Name == "TypeScript");
        Assert.Equal(75.0, csharp.Percent);
        Assert.Equal(25.0, ts.Percent);
    }

    [Fact]
    public async Task LimitsTopLanguagesToFive()
    {
        var repos = Enumerable.Range(1, 6).Select(i => CreateRepo(language: $"Lang{i}")).ToList();
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(5, result.Repositories.TopLanguages.Count);
    }

    [Fact]
    public async Task OrdersTopLanguagesByPercentDescending()
    {
        var repos = new List<Repository>
        {
            CreateRepo(language: "C#"),
            CreateRepo(language: "C#"),
            CreateRepo(language: "TypeScript"),
        };
        var sut = CreateSut(CreateProfile(), repos, []);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal("C#", result.Repositories.TopLanguages[0].Name);
    }

    [Fact]
    public async Task CalculatesTotalEventsCorrectly()
    {
        var events = new List<Activity>
        {
            CreatePushEvent(commitCount: 0),
            CreateEvent("PullRequestEvent"),
            CreateEvent("IssuesEvent"),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(3, result.Activity.TotalEvents);
    }

    [Fact]
    public async Task SumsCommitsAcrossPushEvents()
    {
        var events = new List<Activity>
        {
            CreatePushEvent(commitCount: 3),
            CreatePushEvent(commitCount: 2),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(5, result.Activity.Commits);
    }

    [Fact]
    public async Task CountsPullRequestEventsCorrectly()
    {
        var events = new List<Activity>
        {
            CreateEvent("PullRequestEvent"),
            CreateEvent("PullRequestEvent"),
            CreatePushEvent(commitCount: 0),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(2, result.Activity.PullRequests);
    }

    [Fact]
    public async Task CountsReviewEventsCorrectly()
    {
        var events = new List<Activity>
        {
            CreateEvent("PullRequestReviewEvent"),
            CreateEvent("PullRequestReviewEvent"),
            CreateEvent("PullRequestReviewEvent"),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(3, result.Activity.Reviews);
    }

    [Fact]
    public async Task CountsIssueEventsCorrectly()
    {
        var events = new List<Activity> { CreateEvent("IssuesEvent") };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(1, result.Activity.Issues);
    }

    [Fact]
    public async Task GroupsContributionGraphByWeek()
    {
        // 2026-01-05 is Monday; 2026-01-06 is Tuesday — same week
        var monday = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var tuesday = new DateTimeOffset(2026, 1, 6, 0, 0, 0, TimeSpan.Zero);
        var events = new List<Activity>
        {
            CreateEvent("IssuesEvent", createdAt: monday),
            CreateEvent("IssuesEvent", createdAt: tuesday),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Single(result.ContributionGraph);
        Assert.Equal(2, result.ContributionGraph[0].Count);
    }

    [Fact]
    public async Task PlacesEachWeekInSeparateEntry()
    {
        var week1 = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var week2 = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero);
        var events = new List<Activity>
        {
            CreateEvent("IssuesEvent", createdAt: week1),
            CreateEvent("IssuesEvent", createdAt: week2),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(2, result.ContributionGraph.Count);
    }

    [Fact]
    public async Task OrdersContributionGraphChronologically()
    {
        var week1 = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var week2 = new DateTimeOffset(2026, 1, 12, 0, 0, 0, TimeSpan.Zero);
        var events = new List<Activity>
        {
            CreateEvent("IssuesEvent", createdAt: week2),
            CreateEvent("IssuesEvent", createdAt: week1),
        };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.True(result.ContributionGraph[0].Week < result.ContributionGraph[1].Week);
    }

    [Fact]
    public async Task SetsContributionWeekToMondayOfEventDate()
    {
        // 2026-01-07 is Wednesday — week should be anchored to Monday 2026-01-05
        var wednesday = new DateTimeOffset(2026, 1, 7, 0, 0, 0, TimeSpan.Zero);
        var events = new List<Activity> { CreateEvent("IssuesEvent", createdAt: wednesday) };
        var sut = CreateSut(CreateProfile(), [], events);

        var result = await sut.GetAnalyticsAsync("user");

        Assert.Equal(new DateOnly(2026, 1, 5), result.ContributionGraph[0].Week);
    }
}
