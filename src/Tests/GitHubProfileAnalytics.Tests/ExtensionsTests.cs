using GitHubProfileAnalytics.Extensions;
using Microsoft.Extensions.Configuration;

namespace GitHubProfileAnalytics.Tests;

public class ExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void GetRequiredReturnsValueWhenKeyExists()
    {
        IConfiguration config = BuildConfig(
            new Dictionary<string, string?> { ["MyKey"] = "myvalue" }
        );

        string result = config.GetRequired("MyKey");

        Assert.Equal("myvalue", result);
    }

    [Fact]
    public void GetRequiredThrowsWhenKeyMissing()
    {
        IConfiguration config = BuildConfig([]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            config.GetRequired("Missing:Key")
        );

        Assert.Contains("Missing:Key", ex.Message);
    }

    [Fact]
    public void GetThresholdReturnsOffsetBasedOnConfiguredTtl()
    {
        IConfiguration config = BuildConfig(
            new Dictionary<string, string?> { ["Cache:TtlHours"] = "2" }
        );
        DateTimeOffset before = DateTimeOffset.UtcNow.AddHours(-2);

        DateTimeOffset threshold = CacheHelper.GetThreshold(config, "Cache:TtlHours");

        DateTimeOffset after = DateTimeOffset.UtcNow.AddHours(-2);
        Assert.InRange(threshold, before, after);
    }

    [Fact]
    public void GetThresholdUsesDefaultWhenKeyMissing()
    {
        IConfiguration config = BuildConfig([]);
        DateTimeOffset before = DateTimeOffset.UtcNow.AddHours(-1);

        DateTimeOffset threshold = CacheHelper.GetThreshold(config, "Missing:Key");

        DateTimeOffset after = DateTimeOffset.UtcNow.AddHours(-1);
        Assert.InRange(threshold, before, after);
    }
}
