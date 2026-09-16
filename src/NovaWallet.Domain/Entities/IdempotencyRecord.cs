using NovaWallet.Domain.Enums;

namespace NovaWallet.Domain.Entities;

public class IdempotencyRecord
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public IdempotencyStatus Status { get; set; }

    public int? ResponseStatusCode { get; set; }

    public string? ResponseBodyJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}