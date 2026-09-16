using Microsoft.EntityFrameworkCore;
using NovaWallet.Infrastructure.Persistence;

namespace NovaWallet.Api.Startup;

public class DatabaseMigrationHostedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
        {
            logger.LogInformation("Automatic database migration is disabled.");
            return;
        }

        var retryCount = configuration.GetValue("Database:MigrationRetryCount", 12);
        var retryDelay = TimeSpan.FromSeconds(configuration.GetValue("Database:MigrationRetryDelaySeconds", 5));

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();

                logger.LogInformation("Applying database migrations. Attempt {Attempt} of {RetryCount}.", attempt, retryCount);
                await dbContext.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied.");
                return;
            }
            catch (Exception exception) when (attempt < retryCount)
            {
                logger.LogWarning(exception, "Database migration failed. Retrying in {RetryDelaySeconds} seconds.", retryDelay.TotalSeconds);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        await using var finalScope = serviceProvider.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<NovaWalletDbContext>();
        await finalDbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
