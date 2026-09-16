using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Domain.Entities
{
    public class FinancialLimit
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public long AmountKobo { get; set; }

        public bool IsActive { get; set; }
    }
}
