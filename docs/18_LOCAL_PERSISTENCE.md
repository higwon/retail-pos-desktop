# Local Persistence Architecture

This document defines the local persistence architecture for the Retail POS Desktop application.

## Goal

The desktop POS client must continue operating when the network is unavailable. Local persistence exists to support:

- Product lookup while offline
- Completed order storage
- Recoverable pending checkout state
- Upload/status queue records for later synchronization

## Layer Ownership

```text
RetailPOS.Desktop
    |
    v
RetailPOS.Application
    - Repository contracts
    - Use-case/service contracts
    |
    v
RetailPOS.Infrastructure
    - SQLite implementation
    - Entity mapping
    - Repository implementations
    |
    v
SQLite local database
```

## Rules

- Domain models must not depend on SQLite, EF Core, WPF, or API details.
- Application defines repository contracts.
- Infrastructure implements repository contracts.
- Desktop composes Infrastructure through DI.
- SQLite entities may be different from Domain models.
- Local database files must not be committed.

## Initial SQLite Scope

- Products
- Orders
- Order lines
- Payments
- Pending checkouts
- Sync queue records

## Out of Scope for EPIC-04

- Real server synchronization
- Background retry worker
- API client implementation
- UI binding to persisted data
- Large-scale performance tuning

## Persistence Startup

For development, the app may create or migrate a local database during startup.

The local database path should be configurable later. During early development, a simple local app-data path or development path is acceptable as long as generated database files are ignored by Git.
