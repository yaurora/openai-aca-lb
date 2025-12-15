using Microsoft.Extensions.Configuration;

namespace openai_loadbalancer.Tests;

public class ExposeBackendHeaderTests
{
    [Fact]
    public void LoadConfig_SetsExposeBackendHeader_WhenEnvVarTrue()
    {
        var previous = Environment.GetEnvironmentVariable("EXPOSE_BACKEND_HEADER");
        try
        {
            Environment.SetEnvironmentVariable("EXPOSE_BACKEND_HEADER", "true");

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BACKEND_1_URL"] = "https://example.test/",
                    ["BACKEND_1_PRIORITY"] = "1",
                    ["BACKEND_1_DEPLOYMENT_NAME"] = "gpt-5",
                })
                .Build();

            _ = BackendConfig.LoadConfig(config);

            Assert.True(BackendConfig.ExposeBackendHeader);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EXPOSE_BACKEND_HEADER", previous);
        }
    }

    [Fact]
    public void LoadConfig_DefaultsExposeBackendHeaderToFalse_WhenEnvVarMissing()
    {
        var previous = Environment.GetEnvironmentVariable("EXPOSE_BACKEND_HEADER");
        try
        {
            Environment.SetEnvironmentVariable("EXPOSE_BACKEND_HEADER", null);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BACKEND_1_URL"] = "https://example.test/",
                    ["BACKEND_1_PRIORITY"] = "1",
                })
                .Build();

            _ = BackendConfig.LoadConfig(config);

            Assert.False(BackendConfig.ExposeBackendHeader);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EXPOSE_BACKEND_HEADER", previous);
        }
    }
}

