using Yarp.ReverseProxy.Forwarder;

namespace openai_loadbalancer.Tests;

public class YarpConfigurationTests
{
    [Fact]
    public void GetClusters_DisablesActivityTimeout_WhenConfiguredZero()
    {
        var previousTimeout = BackendConfig.HttpTimeoutSeconds;
        try
        {
            BackendConfig.HttpTimeoutSeconds = 0;
            var config = new YarpConfiguration(new Dictionary<string, BackendConfig>
            {
                ["BACKEND_1"] = new BackendConfig { Url = "https://example.test", Priority = 1 }
            });

            var cluster = config.GetClusters().Single();
            var httpRequest = cluster.HttpRequest ?? new ForwarderRequestConfig();

            Assert.Null(httpRequest.ActivityTimeout);
        }
        finally
        {
            BackendConfig.HttpTimeoutSeconds = previousTimeout;
        }
    }

    [Fact]
    public void GetClusters_SetsActivityTimeout_WhenConfiguredPositive()
    {
        var previousTimeout = BackendConfig.HttpTimeoutSeconds;
        try
        {
            BackendConfig.HttpTimeoutSeconds = 123;
            var config = new YarpConfiguration(new Dictionary<string, BackendConfig>
            {
                ["BACKEND_1"] = new BackendConfig { Url = "https://example.test", Priority = 1 }
            });

            var cluster = config.GetClusters().Single();
            var httpRequest = cluster.HttpRequest ?? new ForwarderRequestConfig();

            Assert.Equal(TimeSpan.FromSeconds(123), httpRequest.ActivityTimeout);
        }
        finally
        {
            BackendConfig.HttpTimeoutSeconds = previousTimeout;
        }
    }
}

