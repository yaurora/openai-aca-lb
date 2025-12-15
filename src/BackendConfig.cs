namespace openai_loadbalancer;

public class BackendConfig
{
    public static int HttpTimeoutSeconds = 100;

    public static Version? HttpRequestVersion { get; private set; }
    public static System.Net.Http.HttpVersionPolicy? HttpRequestVersionPolicy { get; private set; }
    public static bool ExposeBackendHeader { get; private set; }

    public required string Url { get; set; }
    public string? DeploymentName { get; set; }
    public int Priority { get; set; }
    public string? ApiKey { get; set; }

    public static IReadOnlyDictionary<string, BackendConfig> LoadConfig(IConfiguration config)
    {
        var returnDictionary = new Dictionary<string, BackendConfig>();

        var environmentVariables = config.AsEnumerable().Where(x => x.Key.ToUpperInvariant().StartsWith("BACKEND_")).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        var numberOfBackends = environmentVariables.Select(x => x.Key.Split('_')[1]).Distinct();

        if (environmentVariables.Count() == 0 || numberOfBackends.Count() == 0)
        {
            throw new Exception("Could not find any environment variable starting with 'BACKEND_[x]'... please define your backend endpoints");
        }

        foreach (var backendIndex in numberOfBackends)
        {
            var key = $"BACKEND_{backendIndex}";
            var url = LoadEnvironmentVariable(environmentVariables, backendIndex, "URL");
            var deploymentName = LoadEnvironmentVariable(environmentVariables, backendIndex, "DEPLOYMENT_NAME", isMandatory: false);
            var apiKey = LoadEnvironmentVariable(environmentVariables, backendIndex, "APIKEY", isMandatory: false);
            var priority = Convert.ToInt32(LoadEnvironmentVariable(environmentVariables, backendIndex, "PRIORITY"));

            returnDictionary.Add(key, new BackendConfig { Url = url, ApiKey = apiKey, Priority = priority, DeploymentName = deploymentName });
        }

        //Load the general settings not in scope only for specific backends
        var httpTimeout = Environment.GetEnvironmentVariable("HTTP_TIMEOUT_SECONDS");

        if (httpTimeout != null)
        {
            HttpTimeoutSeconds = Convert.ToInt32(httpTimeout);
        }

        var httpRequestVersion = Environment.GetEnvironmentVariable("HTTP_REQUEST_VERSION");
        if (!string.IsNullOrWhiteSpace(httpRequestVersion))
        {
            HttpRequestVersion = ParseHttpVersion(httpRequestVersion);
        }

        var httpRequestVersionPolicy = Environment.GetEnvironmentVariable("HTTP_REQUEST_VERSION_POLICY");
        if (!string.IsNullOrWhiteSpace(httpRequestVersionPolicy))
        {
            HttpRequestVersionPolicy = ParseHttpVersionPolicy(httpRequestVersionPolicy);
        }

        var exposeBackendHeader = Environment.GetEnvironmentVariable("EXPOSE_BACKEND_HEADER");
        ExposeBackendHeader = string.Equals(exposeBackendHeader?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

        return returnDictionary;
    }

    internal static TimeSpan? GetActivityTimeout()
    {
        return HttpTimeoutSeconds <= 0 ? null : TimeSpan.FromSeconds(HttpTimeoutSeconds);
    }

    internal static Version ParseHttpVersion(string raw)
    {
        var value = raw.Trim();

        return value switch
        {
            "1.0" => System.Net.HttpVersion.Version10,
            "1.1" => System.Net.HttpVersion.Version11,
            "2.0" => System.Net.HttpVersion.Version20,
            _ => throw new ArgumentException($"Invalid HTTP_REQUEST_VERSION '{raw}'. Valid values: 1.0, 1.1, 2.0")
        };
    }

    internal static System.Net.Http.HttpVersionPolicy ParseHttpVersionPolicy(string raw)
    {
        if (Enum.TryParse<System.Net.Http.HttpVersionPolicy>(raw.Trim(), ignoreCase: true, out var policy))
        {
            return policy;
        }

        throw new ArgumentException($"Invalid HTTP_REQUEST_VERSION_POLICY '{raw}'. Valid values: RequestVersionOrLower, RequestVersionOrHigher, RequestVersionExact");
    }

    private static string? LoadEnvironmentVariable(IDictionary<string, string?> variables, string backendIndex, string property, bool isMandatory = true)
    {
        var key = $"BACKEND_{backendIndex}_{property}";

        if (!variables.TryGetValue(key, out var value) && isMandatory)
        {
            throw new Exception($"Missing environment variable {key}");
        }

        if (value != null)
        {
            return value.Trim();
        }
        else
        {
            return null;
        }
    }
}
    
