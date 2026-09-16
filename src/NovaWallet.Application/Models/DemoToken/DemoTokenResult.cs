using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Models.DemoToken
{
    public class DemoTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
