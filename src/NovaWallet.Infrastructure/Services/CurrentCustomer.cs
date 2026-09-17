using Microsoft.AspNetCore.Http;
using NovaWallet.Application.Abstraction;

namespace NovaWallet.Infrastructure.Services;

public class CurrentCustomer : ICurrentCustomer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCustomer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CustomerId => _httpContextAccessor.HttpContext?.User.FindFirst("customer_id")?.Value;
}
