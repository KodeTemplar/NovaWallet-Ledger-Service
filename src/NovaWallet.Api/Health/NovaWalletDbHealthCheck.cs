using Microsoft.Extensions.Diagnostics.HealthChecks;
using NovaWallet.Infrastructure.Persistence;

namespace NovaWallet.Api.Health;

public sealed class NovaWalletDbHealthCheck : IHealthCheck
{
    private readonly NovaWalletDbContext _dbContext;

    public NovaWalletDbHealthCheck(NovaWalletDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database is unavailable.");
    }
}
