using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Abstraction
{
    public interface IFinancialLimitService
    {
        Task<long> GetDailyOutboundTransferLimitKoboAsync(CancellationToken cancellationToken = default);
    }
}
