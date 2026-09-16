using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Models.Audit
{
    public class AuditRequest
    {
        public Guid WalletId { get; set; }
        public Guid? TransactionId { get; set; }
        public string MutationType { get; set; } = string.Empty;
        public long AmountKobo { get; set; }
        public long BalanceBeforeKobo { get; set; }
        public long BalanceAfterKobo { get; set; }
        public string ActorCustomerId { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
    }
}
