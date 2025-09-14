using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CodeReviewRunner.HealthChecks;

public class AzureDevOpsHealthCheck : IHealthCheck
{
    private readonly ILogger<AzureDevOpsHealthCheck> _logger;

    public AzureDevOpsHealthCheck(ILogger<AzureDevOpsHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if PAT token is available
            var pat = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
            if (string.IsNullOrWhiteSpace(pat))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("SYSTEM_ACCESSTOKEN is not set"));
            }

            // Check if required environment variables are set
            var requiredVars = new[] { "SYSTEM_ACCESSTOKEN" };
            var missingVars = requiredVars.Where(var => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(var))).ToList();

            if (missingVars.Any())
            {
                return Task.FromResult(HealthCheckResult.Unhealthy($"Missing environment variables: {string.Join(", ", missingVars)}"));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Azure DevOps configuration is valid"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("Health check failed", ex));
        }
    }
}
