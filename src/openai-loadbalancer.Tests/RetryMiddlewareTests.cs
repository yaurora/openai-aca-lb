using Microsoft.AspNetCore.Http;

namespace openai_loadbalancer.Tests;

public class RetryMiddlewareTests
{
    [Theory]
    [InlineData(429, true, false, true)]
    [InlineData(500, true, false, true)]
    [InlineData(200, true, false, false)]
    [InlineData(500, false, false, false)]
    [InlineData(500, true, true, false)]
    public void ShouldRetry_FollowsExpectedRules(int statusCode, bool atLeastOneHealthy, bool requestAborted, bool expected)
    {
        var actual = RetryMiddleware.ShouldRetry(statusCode, atLeastOneHealthy, requestAborted);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/openai/deployments/gpt-5/chat/completions", "gpt-5")]
    [InlineData("/openai/deployments/GPT-5/chat/completions", "GPT-5")]
    [InlineData("/openai/deployments/gpt-4.1", "gpt-4.1")]
    public void TryGetRequestedDeploymentName_ParsesDeployment(string path, string expected)
    {
        var ok = RetryMiddleware.TryGetRequestedDeploymentName(new PathString(path), out var deployment);

        Assert.True(ok);
        Assert.Equal(expected, deployment);
    }

    [Theory]
    [InlineData("/openai/models")]
    [InlineData("/openai/deployments")]
    [InlineData("/openai/deployments/")]
    [InlineData("/other/deployments/gpt-5/chat/completions")]
    [InlineData("")]
    public void TryGetRequestedDeploymentName_ReturnsFalse_WhenNotADeploymentPath(string path)
    {
        var ok = RetryMiddleware.TryGetRequestedDeploymentName(new PathString(path), out var deployment);

        Assert.False(ok);
        Assert.Equal(string.Empty, deployment);
    }
}
