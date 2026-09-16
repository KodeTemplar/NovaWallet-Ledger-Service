using NovaWallet.Api.Abstractions;

namespace NovaWallet.Api.Services;

public class CurrentCustomer : ICurrentCustomer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentCustomer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CustomerId => _httpContextAccessor.HttpContext?.User.FindFirst("customer_id")?.Value;
}
