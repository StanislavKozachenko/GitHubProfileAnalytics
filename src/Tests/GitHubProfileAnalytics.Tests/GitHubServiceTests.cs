using GitHubProfileAnalytics.Services.GitHub;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Octokit;

namespace GitHubProfileAnalytics.Tests;

public class GitHubServiceTests
{
    private static User MakeFakeUser() =>
        new(
            avatarUrl: "https://avatar.url",
            bio: "bio text",
            blog: null,
            collaborators: 0,
            company: null,
            createdAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            updatedAt: DateTimeOffset.UtcNow,
            diskUsage: 0,
            email: null,
            followers: 10,
            following: 5,
            hireable: null,
            htmlUrl: null,
            totalPrivateRepos: 0,
            id: 1,
            location: null,
            login: "testuser",
            name: "Test User",
            nodeId: null,
            ownedPrivateRepos: 0,
            plan: null,
            privateGists: 0,
            publicGists: 0,
            publicRepos: 3,
            url: null,
            permissions: null,
            siteAdmin: false,
            ldapDistinguishedName: null,
            suspendedAt: null
        );

    private static IGitHubClient MakeClient(string username, User user)
    {
        var usersClient = Substitute.For<IUsersClient>();
        usersClient.Get(username).Returns(user);

        var client = Substitute.For<IGitHubClient>();
        client.User.Returns(usersClient);

        return client;
    }

    [Fact]
    public async Task GetProfileAsyncReturnsMappedDto()
    {
        var fakeUser = MakeFakeUser();
        var service = new GitHubService(MakeClient("testuser", fakeUser));

        var result = await service.GetProfileAsync("testuser");

        Assert.Equal("testuser", result.Login);
        Assert.Equal("Test User", result.Name);
        Assert.Equal("https://avatar.url", result.AvatarUrl);
        Assert.Equal("bio text", result.Bio);
        Assert.Equal(3, result.PublicRepos);
        Assert.Equal(10, result.Followers);
        Assert.Equal(5, result.Following);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), result.CreatedAt);
    }

    [Fact]
    public async Task GetProfileAsyncPropagatesNotFoundException()
    {
        var usersClient = Substitute.For<IUsersClient>();
        usersClient
            .Get("unknown")
            .Throws(new NotFoundException("Not found", System.Net.HttpStatusCode.NotFound));

        var client = Substitute.For<IGitHubClient>();
        client.User.Returns(usersClient);

        var service = new GitHubService(client);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProfileAsync("unknown"));
    }
}
