using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Domain.Constants
{
    public static class ApplicationRoles
    {
        public const string NipSimulator = "nip_simulator";
        public const string System = "system";
        public const string Admin = "admin";

        public static readonly string[] All =
        {
            NipSimulator,
            System,
            Admin
        };
    }
}
