namespace openai_loadbalancer.Tests;

public class BackendConfigTests
{
    [Theory]
    [InlineData("1.0", 1, 0)]
    [InlineData("1.1", 1, 1)]
    [InlineData("2.0", 2, 0)]
    public void ParseHttpVersion_ParsesKnownVersions(string raw, int expectedMajor, int expectedMinor)
    {
        var version = BackendConfig.ParseHttpVersion(raw);

        Assert.Equal(expectedMajor, version.Major);
        Assert.Equal(expectedMinor, version.Minor);
    }

    [Fact]
    public void ParseHttpVersion_ThrowsOnInvalid()
    {
        var ex = Assert.Throws<ArgumentException>(() => BackendConfig.ParseHttpVersion("3.0"));
        Assert.Contains("HTTP_REQUEST_VERSION", ex.Message);
    }

    [Theory]
    [InlineData("RequestVersionOrLower")]
    [InlineData("RequestVersionOrHigher")]
    [InlineData("RequestVersionExact")]
    [InlineData("requestversionorlower")]
    public void ParseHttpVersionPolicy_ParsesKnownPolicies(string raw)
    {
        var policy = BackendConfig.ParseHttpVersionPolicy(raw);

        Assert.True(Enum.IsDefined(policy));
    }

    [Fact]
    public void ParseHttpVersionPolicy_ThrowsOnInvalid()
    {
        var ex = Assert.Throws<ArgumentException>(() => BackendConfig.ParseHttpVersionPolicy("Nope"));
        Assert.Contains("HTTP_REQUEST_VERSION_POLICY", ex.Message);
    }
}

