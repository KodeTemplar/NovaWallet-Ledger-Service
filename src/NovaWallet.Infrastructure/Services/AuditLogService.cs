using NovaWallet.Application.Abstraction;
using NovaWallet.Application.Models.Audit;
using NovaWallet.Domain.Entities;
using NovaWallet.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Infrastructure.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly NovaWalletDbContext _dbContext;

        public AuditLogService(NovaWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task AddAsync(AuditRequest request, CancellationToken cancellationToken = default)
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                WalletId = request.WalletId,
                TransactionId = request.TransactionId,
                MutationType = request.MutationType,
                AmountKobo = request.AmountKobo,
                BalanceBeforeKobo = request.BalanceBeforeKobo,
                BalanceAfterKobo = request.BalanceAfterKobo,
                ActorCustomerId = request.ActorCustomerId,
                MetadataJson = request.MetadataJson,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            _dbContext.AuditLogs.Add(auditLog);

            return Task.CompletedTask;
        }
    }
}
