using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Common.Responses
{
    public abstract class ApiResult
    {
        public int StatusCode { get; set; }
    }
}