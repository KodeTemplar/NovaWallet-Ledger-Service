                  HTTP REQUEST
                       │
                       ▼
              ┌─────────────────┐
              │ NovaWallet.Api  │
              │                 │
              │ Controllers     │
              │ JWT             │
              │ Swagger         │
              │ ProblemDetails  │
              └────────┬────────┘
                       │
                       ▼
          ┌─────────────────────────┐
          │ NovaWallet.Application  │
          │                         │
          │ Use cases / Services    │
          │ Business orchestration  │
          └────────────┬────────────┘
                       │
                       ▼
              ┌─────────────────┐
              │NovaWallet.Domain│
              │                 │
              │ Wallet          │
              │ Transaction     │
              │ LedgerEntry     │
              │ AuditLog        │
              │ Rules / Enums   │
              └─────────────────┘
                       ▲
                       │
          ┌────────────┴────────────┐
          │NovaWallet.Infrastructure│
          │                         │
          │ EF Core                 │
          │ DbContext               │
          │ SQL Server              │
          │ Migrations              │
          └─────────────────────────┘