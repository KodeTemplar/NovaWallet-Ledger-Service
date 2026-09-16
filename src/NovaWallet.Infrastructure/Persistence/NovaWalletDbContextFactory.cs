using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NovaWallet.Infrastructure.Persistence;

public class NovaWalletDbContextFactory : IDesignTimeDbContextFactory<NovaWalletDbContext>
{
    public NovaWalletDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NovaWalletDb")
            ?? "Server=localhost,1433;Database=NovaWallet;User Id=sa;Password=Your_strong_password123;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<NovaWalletDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NovaWalletDbContext(options);
    }
}
