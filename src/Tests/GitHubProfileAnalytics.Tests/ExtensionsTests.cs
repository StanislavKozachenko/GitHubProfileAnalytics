using GitHubProfileAnalytics.Extensions;
using Microsoft.Extensions.Configuration;

namespace GitHubProfileAnalytics.Tests;

public class ExtensionsTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void GetRequiredReturnsValueWhenKeyExists()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["MyKey"] = "myvalue" });

        var result = config.GetRequired("MyKey");

        Assert.Equal("myvalue", result);
    }

    [Fact]
    public void GetRequiredThrowsWhenKeyMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var ex = Assert.Throws<InvalidOperationException>(() => config.GetRequired("Missing:Key"));

        Assert.Contains("Missing:Key", ex.Message);
    }

    [Fact]
    public void GetThresholdReturnsOffsetBasedOnConfiguredTtl()
    {
        var config = BuildConfig(new Dictionary<string, string?> { ["Cache:TtlHours"] = "2" });
        var before = DateTimeOffset.UtcNow.AddHours(-2);

        var threshold = CacheHelper.GetThreshold(config, "Cache:TtlHours");

        var after = DateTimeOffset.UtcNow.AddHours(-2);
        Assert.InRange(threshold, before, after);
    }

    [Fact]
    public void GetThresholdUsesDefaultWhenKeyMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var before = DateTimeOffset.UtcNow.AddHours(-1);

        var threshold = CacheHelper.GetThreshold(config, "Missing:Key");

        var after = DateTimeOffset.UtcNow.AddHours(-1);
        Assert.InRange(threshold, before, after);
    }
}
