# AI Usage Disclosure

This document describes how AI tools were used during the NovaWallet Ledger Service assessment. AI was used as a supporting engineering tool and accelerator, not as an autonomous owner of the system.

Architecture ownership, financial correctness, security judgment, implementation acceptance, and final verification remained engineering responsibilities. AI-generated output was treated as a proposal to review and validate, not as an authoritative source of truth.

## Responsible AI Use and Data Handling

A data-minimisation approach was used when working with AI tools. Prompts were scoped to the technical problem being solved: ASP.NET Core architecture, SQL Server persistence, wallet ledger correctness, idempotency, concurrency, tests, audits, and documentation.

Unrelated company/internal information, internal documentation, production data, credentials, secrets, unrelated proprietary code, and unnecessary contextual material were not intentionally supplied where they were not needed for the assessment.

Where repository-aware tooling was used, its access was scoped to the assessment project required for the task. Credentials and secrets were not intentionally included in prompts. Confidential assessment text is not reproduced in this document.

## AI Tools Used

### ChatGPT

ChatGPT was used primarily as a technical discussion and review assistant for:

- interpreting requirements;
- discussing architecture options and trade-offs;
- planning implementation steps;
- reasoning about financial correctness;
- discussing concurrency, idempotency, daily limits, and audit trail design;
- constructing focused implementation and audit instructions;
- reviewing documentation structure.

ChatGPT did not independently design the entire system. Its output was used as input to engineering decisions.

### OpenAI Codex

OpenAI Codex was used as repository-aware engineering assistance for:

- targeted implementation under explicit scope;
- test generation;
- codebase inspection;
- scoped audits;
- documentation generation;
- applying explicitly requested changes to the repository.

Codex-generated changes were reviewed before acceptance and validated against the implementation goals and automated tests.

## Engineering Ownership

| Area | AI Support | Engineering Ownership |
| --- | --- | --- |
| Requirements | Helped decompose the assessment requirements into implementable work. | Final interpretation, prioritisation, and scope decisions remained engineering decisions. |
| Architecture | Helped discuss layered architecture, SQL Server persistence, Docker, and test structure. | Architecture selection and trade-offs were reviewed and accepted based on the assessment needs. |
| Implementation | Generated targeted code under scoped instructions. | Generated changes were inspected, corrected where needed, and accepted only after review. |
| Financial correctness | Assisted with concurrency, idempotency, money representation, daily limit, and audit reasoning. | Correctness invariants and final behavior were validated through code review, SQL Server behavior, and tests. |
| Testing | Assisted with edge-case and integration test ideas. | Test strategy, execution, and interpretation remained engineering responsibilities. |
| Security/data handling | Assisted with checklists and review prompts. | Control over supplied context, auth decisions, role checks, and secret-handling judgment remained engineering responsibilities. |
| Documentation | Assisted with drafting and organization. | Technical accuracy, wording, and final submission remained engineering responsibilities. |

## Concrete Prompt Examples

### Example 1: Architecture and Foundation

**Goal**

Create a maintainable foundation for the service without over-engineering the assessment.

**Representative prompt**

> Design the NovaWallet solution as a maintainable ASP.NET Core API using SQL Server and EF Core. Use separate API, Application, Domain and Infrastructure projects. Keep controllers thin and avoid unnecessary MediatR/CQRS, generic repository or Unit of Work abstractions. All monetary values must use integer kobo. Include JWT authentication, Docker Compose, migrations and integration-test foundations.

**What AI produced**

AI assisted with the initial layered solution structure:

- `NovaWallet.Api`
- `NovaWallet.Application`
- `NovaWallet.Domain`
- `NovaWallet.Infrastructure`
- unit and integration test projects;
- Dockerfile and Docker Compose setup;
- EF Core persistence foundation;
- JWT bearer authentication foundation.

**How the output was reviewed/verified**

The result was reviewed for:

- project dependency direction;
- use of SQL Server and EF Core;
- integer money model;
- database constraints;
- JWT authentication and role policy behavior;
- Docker configuration;
- avoidance of unnecessary CQRS/MediatR/repository abstractions.

Not every generated suggestion was accepted unchanged. The foundation was adjusted as the financial requirements became more concrete.

### Example 2: Concurrency-Safe Transfers

**Goal**

Implement wallet-to-wallet transfers that preserve balances under concurrent requests.

**Representative prompt**

> Implement wallet-to-wallet transfers so balances cannot become negative under concurrent requests. Use a database transaction and SQL Server locking rather than process-level locking. Lock both wallets in deterministic wallet-ID order to reduce deadlock risk. The authenticated customer's wallet must remain the source regardless of lock order. Apply the daily outbound limit inside the same transaction and add integration tests for concurrent transfers and opposite-direction transfers.

**What AI produced**

AI assisted with transfer logic using:

- a database transaction;
- `IsolationLevel.Serializable`;
- SQL Server locking hints such as `UPDLOCK`, `HOLDLOCK`, and `ROWLOCK`;
- deterministic wallet lock ordering;
- sufficient-funds checks after wallet locking;
- daily outbound limit enforcement inside the transaction;
- transaction, ledger, audit, and idempotency mutations;
- concurrent transfer tests.

**How the output was reviewed/verified**

The transfer implementation was reviewed against financial invariants:

- the authenticated customer's wallet must always remain the source;
- lock order must not change business meaning;
- source and destination rows must be mapped by wallet id after locking;
- insufficient-funds checks must happen under lock;
- daily outbound totals must be updated atomically with the transfer;
- replayed idempotent requests must not mutate balances twice.

The implementation was then covered by integration tests for same-source concurrent transfers, daily limit concurrency, opposite-direction concurrent transfers, and idempotent replay.

### Example 3: Financial Hardening

**Goal**

Harden money handling and audit behavior for a financial ledger.

**Representative prompt**

> Audit the financial path for strict integer-kobo compliance and audit completeness. Remove decimal/double/float from monetary request, response and calculation paths. Use long AmountKobo directly, preserve idempotency semantics, ensure every committed balance mutation has an audit record, and prevent existing audit records from being modified or deleted through EF Core.

**What AI produced**

AI assisted with:

- `long AmountKobo` request/response fields;
- SQL `bigint` financial columns;
- integer-only validation and formatting;
- audit records for wallet creation, credits, and transfers;
- an EF Core `SaveChanges` / `SaveChangesAsync` guard preventing modification or deletion of existing audit records;
- unit and integration tests for integer money and audit immutability.

**How the output was reviewed/verified**

The financial path was checked for floating-point types, audited through source inspection, and validated with tests covering amount validation, integer formatting, audit insert/update/delete behavior, and transfer/credit audit completeness.

## A Specific AI-Generated Defect I Caught

The most important AI mistake involved deterministic wallet locking for transfers.

AI correctly understood the high-level strategy: lock both wallets in deterministic wallet-ID order to reduce deadlock risk. However, an intermediate AI-generated version introduced a subtle defect when mapping the locked rows back to their business roles.

The unsafe logic was effectively:

```csharp
var sourceWallet = firstWallet?.Id == sourceId ? firstWallet : secondWallet;
var destinationWallet = firstWallet?.Id == destinationId ? firstWallet : secondWallet;
```

The issue is that `secondWallet` was used as a fallback without proving it actually matched the requested business role. Deterministic lock order is not the same thing as transfer direction. The lower wallet id might be either the source or the destination.

In a financial system, that distinction matters. If row-lock order and business role mapping are confused, a transfer implementation can debit or credit the wrong wallet, incorrectly reject a valid operation, or hide a missing-wallet case.

## How the Defect Was Caught

The issue was caught during manual review of the transfer flow and concurrency design. The key question was:

> After locking wallets by sorted ID order, how do we prove which locked row is the source and which is the destination?

That review showed that deterministic lock ordering only solves lock acquisition order. It does not by itself preserve business meaning. The locked rows still need to be explicitly mapped back to source and destination by wallet id.

The issue was also covered by strengthening tests around opposite-direction concurrent transfers and balance correctness.

## How the Defect Was Corrected

The mapping was corrected so each business role is assigned only when the locked row id matches the expected id:

```csharp
var sourceWallet = firstWallet?.Id == sourceId
    ? firstWallet
    : secondWallet?.Id == sourceId
        ? secondWallet
        : null;

var destinationWallet = firstWallet?.Id == destinationId
    ? firstWallet
    : secondWallet?.Id == destinationId
        ? secondWallet
        : null;
```

The current implementation uses this safer pattern and then explicitly handles `null` source or destination wallets before applying balance mutations.

The lesson was that AI can produce code that is directionally plausible but unsafe in a financial context. The high-level idea was useful, but the generated implementation still needed careful review against the underlying invariant: the authenticated customer's wallet must remain the source regardless of lock order.

## Verification Approach

AI-generated output was reviewed using multiple checks:

- source review of the affected financial path;
- verification of money types and SQL column types;
- database constraint review;
- tests for validation and edge cases;
- SQL Server integration tests through Testcontainers;
- concurrency tests for transfer and daily-limit behavior;
- idempotency replay and conflict tests;
- audit completeness and audit immutability tests;
- final documentation audits against the actual repository.

The final local verified test run after Docker was available was:

```text
56 total
56 passed
0 failed
0 skipped
```

## Summary

AI was useful for accelerating implementation, test coverage, review checklists, and documentation. The assessment still required engineering judgment, especially around financial correctness, concurrency, idempotency, audit integrity, and data handling.

The deterministic lock-mapping defect is the clearest example: AI produced a plausible implementation pattern, but review against the financial invariants found the unsafe edge. The final implementation reflects the reviewed and corrected behavior rather than accepting AI output blindly.
