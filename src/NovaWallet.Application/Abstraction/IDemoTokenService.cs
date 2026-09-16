using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Abstraction
{
    public interface IDemoTokenService
    {
        Task<ApiResult> CreateTokenAsync(string customerId, string[]? roles);
    }
}
