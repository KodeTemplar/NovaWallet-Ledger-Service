using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Application.Abstraction;
using NovaWallet.Infrastructure.Persistence;
using NovaWallet.Infrastructure.Services;

namespace NovaWallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NovaWalletDb")
            ?? throw new InvalidOperationException("Connection string 'NovaWalletDb' is not configured.");

        services.AddDbContext<NovaWalletDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IFinancialLimitService, FinancialLimitService>();
        services.AddScoped<IDemoTokenService, DemoTokenService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
