using NovaWallet.Application.Models.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Abstraction
{
    public interface IAuditLogService
    {
        Task AddAsync(AuditRequest request, CancellationToken cancellationToken = default);
    }
}
