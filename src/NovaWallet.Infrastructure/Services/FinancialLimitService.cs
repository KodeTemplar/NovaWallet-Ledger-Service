using Microsoft.EntityFrameworkCore;
using NovaWallet.Application.Abstraction;
using NovaWallet.Domain.Constants;
using NovaWallet.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Infrastructure.Services
{
    public class FinancialLimitService : IFinancialLimitService
    {
        private readonly NovaWalletDbContext _dbContext;

        public FinancialLimitService(NovaWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<long> GetDailyOutboundTransferLimitKoboAsync(
            CancellationToken cancellationToken = default)
        {
            var limit = await _dbContext.FinancialLimits
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Code == FinancialLimitCodes.DailyOutboundTransfer && x.IsActive, cancellationToken);

            if (limit is null)
            {
                throw new InvalidOperationException("Daily outbound transfer limit is not configured.");
            }

            return limit.AmountKobo;
        }
    }
}
