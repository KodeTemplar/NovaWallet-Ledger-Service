using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Models.DemoToken
{
    public class DemoTokenRequest
    {
        public string CustomerId { get; set; } = string.Empty;

        public string[]? Roles { get; set; }
    }
}
