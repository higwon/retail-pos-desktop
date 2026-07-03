# ADR-004: Use EF Core for Local SQLite Persistence

## Status

Accepted

## Context

The desktop client needs SQLite persistence for products, orders, pending checkouts,
and the synchronization queue. The Domain and Application layers must remain independent
from persistence frameworks.

## Decision

Use Entity Framework Core with the SQLite provider inside `RetailPOS.Infrastructure`.
Use explicit Infrastructure entities and mappers instead of persistence attributes on
Domain models. Use EF Core migrations for schema evolution.

## Reasoning

- EF Core supports SQLite transactions and migrations.
- A shared scoped `DbContext` can provide the required atomic checkout transaction.
- Explicit entities keep database concerns out of Domain.
- The approach integrates cleanly with Microsoft dependency injection.

## Consequences

Positive:

- Consistent mapping, migration, and transaction infrastructure.
- Repository integration tests can use temporary SQLite databases.
- Later SQL Server infrastructure can use separate mappings without changing Domain.

Trade-offs:

- Infrastructure carries EF Core dependencies and mapping code.
- Repository implementations must avoid leaking `DbContext`, `DbSet`, or `IQueryable`.
- SQLite behavior still requires integration tests; an in-memory LINQ provider is not sufficient.
