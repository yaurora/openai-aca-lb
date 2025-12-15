using Yarp.ReverseProxy.Model;

namespace openai_loadbalancer;

public class RetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Dictionary<string, BackendConfig> _backends;
    private readonly ILogger _logger;

    public RetryMiddleware(RequestDelegate next, Dictionary<string, BackendConfig> backends, ILoggerFactory loggerFactory)
    {
        _next = next;
        _backends = backends;
        _logger = loggerFactory.CreateLogger<RetryMiddleware>();
    }

    /// <summary>
    /// The code in this method is based on comments from https://github.com/microsoft/reverse-proxy/issues/56
    /// When YARP natively supports retries, this will probably be greatly simplified.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        var shouldRetry = true;
        var retryCount = 0;

        while (shouldRetry)
        {
            if (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request aborted before proxying (attempt {Attempt}).", retryCount + 1);
                return;
            }

            var reverseProxyFeature = context.GetReverseProxyFeature();
            var destination = PickOneDestination(context);

            if (destination == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                var hasDeployment = TryGetRequestedDeploymentName(context.Request.Path, out var deployment);
                var message = hasDeployment
                    ? $"No backend configured for requested deployment '{deployment}'."
                    : "No backend configured for requested deployment.";

                await context.Response.WriteAsync(message);
                return;
            }

            var hasRequestedDeploymentName = TryGetRequestedDeploymentName(context.Request.Path, out var requestedDeploymentName);
            var backend = _backends[destination.DestinationId];

            _logger.LogInformation(
                "Proxy attempt {Attempt}: deployment={Deployment} backend={BackendId} url={BackendUrl} priority={Priority}",
                retryCount + 1,
                hasRequestedDeploymentName ? requestedDeploymentName : "(none)",
                destination.DestinationId,
                backend.Url,
                backend.Priority);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            reverseProxyFeature.AvailableDestinations = new List<DestinationState> { destination };

            if (retryCount > 0)
            {
                //If this is a retry, we must reset the request body to initial position and clear the current response
                context.Request.Body.Position = 0;
                reverseProxyFeature.ProxiedDestination = null;
                context.Response.Clear();
            }

            await _next(context);

            if (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Request aborted while proxying (attempt {Attempt}, elapsedMs={ElapsedMs}).",
                    retryCount + 1,
                    stopwatch.ElapsedMilliseconds);
                return;
            }

            var statusCode = context.Response.StatusCode;
            var atLeastOneBackendHealthy = GetNumberHealthyEndpoints(context) > 0;
            retryCount++;

            shouldRetry = ShouldRetry(statusCode, atLeastOneBackendHealthy, requestAborted: false);

            _logger.LogInformation(
                "Proxy attempt {Attempt} completed: status={StatusCode} elapsedMs={ElapsedMs} willRetry={WillRetry}",
                retryCount,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                shouldRetry);
        }
    }

    internal static bool ShouldRetry(int statusCode, bool atLeastOneBackendHealthy, bool requestAborted)
    {
        if (requestAborted)
        {
            return false;
        }

        return (statusCode is 429 or >= 500) && atLeastOneBackendHealthy;
    }

    private static int GetNumberHealthyEndpoints(HttpContext context)
    {
        return context.GetReverseProxyFeature().AllDestinations.Count(m => m.Health.Passive is DestinationHealth.Healthy or DestinationHealth.Unknown);
    }


    /// <summary>
    /// The native YARP ILoadBalancingPolicy interface does not play well with HTTP retries, that's why we're adding this custom load-balancing code.
    /// This needs to be reevaluated to a ILoadBalancingPolicy implementation when YARP supports natively HTTP retries.
    /// </summary>
    private DestinationState? PickOneDestination(HttpContext context)
    {
        var reverseProxyFeature = context.GetReverseProxyFeature();
        var allDestinations = reverseProxyFeature.AllDestinations;

        if (allDestinations.Count == 0)
        {
            _logger.LogWarning("No destinations available in reverse proxy feature.");
            return null;
        }

        var requestedDeploymentName = TryGetRequestedDeploymentName(context.Request.Path, out var deploymentName) ? deploymentName : null;

        // If the request targets a specific deployment, only consider backends configured for that deployment.
        // If none match at all, fail fast rather than silently routing to a different deployment.
        var candidateIndexes = requestedDeploymentName == null
            ? Enumerable.Range(0, allDestinations.Count).ToArray()
            : Enumerable.Range(0, allDestinations.Count)
                .Where(i =>
                {
                    var destinationId = allDestinations[i].DestinationId;
                    if (!_backends.TryGetValue(destinationId, out var backend))
                    {
                        return false;
                    }

                    return backend.DeploymentName != null &&
                           string.Equals(backend.DeploymentName, requestedDeploymentName, StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();

        if (requestedDeploymentName != null && candidateIndexes.Length == 0)
        {
            _logger.LogWarning("No backend configured for requested deployment '{DeploymentName}'", requestedDeploymentName);
            return null;
        }

        var selectedPriority = int.MaxValue;
        var availableBackends = new List<int>();

        foreach (var i in candidateIndexes)
        {
            var destination = allDestinations[i];

            if (destination.Health.Passive != DestinationHealth.Unhealthy)
            {
                var destinationPriority = _backends[destination.DestinationId].Priority;

                if (destinationPriority < selectedPriority)
                {
                    selectedPriority = destinationPriority;
                    availableBackends.Clear();
                    availableBackends.Add(i);
                }
                else if (destinationPriority == selectedPriority)
                {
                    availableBackends.Add(i);
                }
            }
        }

        int backendIndex;

        if (availableBackends.Count == 1)
        {
            //Returns the only available backend if we have only one available
            backendIndex = availableBackends[0];
        }
        else
        if (availableBackends.Count > 0)
        {
            //Returns a random backend from the list if we have more than one available with the same priority
            backendIndex = availableBackends[Random.Shared.Next(0, availableBackends.Count)];
        }
        else
        {
            // Returns a random backend if all candidates are unhealthy
            _logger.LogWarning("All candidate backends are unhealthy. Picking a random backend...");
            backendIndex = candidateIndexes[Random.Shared.Next(0, candidateIndexes.Length)];
        }

        var pickedDestination = allDestinations[backendIndex];

        return pickedDestination;
    }

    internal static bool TryGetRequestedDeploymentName(PathString path, out string deploymentName)
    {
        deploymentName = string.Empty;

        if (!path.HasValue)
        {
            return false;
        }

        // Expected format: /openai/deployments/{deploymentName}/...
        var segments = path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 3 &&
            string.Equals(segments[0], "openai", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "deployments", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(segments[2]))
        {
            deploymentName = segments[2];
            return true;
        }

        return false;
    }
}
