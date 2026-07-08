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
RetailPOS.Desktop ------> RetailPOS.Infrastructure ------> SQLite local database
        |                          |
        |                          v
        +-----------------> RetailPOS.Application ------> RetailPOS.Domain
```

The arrows above show compile-time dependencies. `RetailPOS.Desktop` composes both
Application and Infrastructure. Infrastructure depends on Application contracts;
Application never depends on Infrastructure.

## Rules

- Domain models must not depend on SQLite, EF Core, WPF, or API details.
- Application defines repository contracts.
- Infrastructure implements repository contracts.
- Desktop composes Infrastructure through DI.
- SQLite entities may be different from Domain models.
- Local database files must not be committed.
- Use EF Core with the SQLite provider for EPIC-04.
- Keep EF Core packages and types inside Infrastructure.

## Initial SQLite Scope

- Products
- Orders
- Order lines
- Payments
- Pending checkouts
- Sync queue records

SQLite stores both upstream caches and local operational records. Products and
categories are production caches sourced from HQ/API sync, while orders, pending
checkouts, receipts derived from orders, and sync queue records are created locally.
See [Data Ownership and Source of Truth](22_DATA_OWNERSHIP.md) for the full rules.

Product cache sync metadata includes server stock, upstream version, and UTC update
timestamp. Product upsert should apply newer or equal upstream versions and ignore
older versions so stale responses do not roll the local cache backward.

## Out of Scope for EPIC-04

- Real server synchronization
- Background retry worker
- API client implementation
- UI binding to persisted data
- Large-scale performance tuning

## Persistence Startup

For development, the app may create or migrate a local database during startup.

The default Windows path is `%LOCALAPPDATA%\RetailPOS\retail-pos.db`. Infrastructure
receives the resolved path through options registered by Desktop, so tests can use a
temporary database path. Generated `.db`, `.db-shm`, `.db-wal`, and SQLite files must
be ignored by Git.
