# ADR-002: Use SQLite for Local Offline Storage

## Status

Accepted

## Context

The POS client must continue selling products when the server or network is unavailable.

The client needs local persistence for:

- Product cache
- Local orders
- Pending checkout recovery state
- Sync queue
- Cached employee authentication data
- Local estimated stock

## Decision

Use SQLite as the local offline database for the desktop client.

## Reasoning

SQLite is a good fit because:

- It is lightweight and easy to deploy with a desktop application.
- It supports transactional writes.
- It requires no separate database server process.
- It works well for local cache and offline-first scenarios.
- It is sufficient for MVP-scale POS local data.

## Consequences

Positive:

- Simple deployment.
- Reliable local persistence.
- Good fit for offline queue and recovery data.

Trade-offs:

- Not a replacement for central server storage.
- Requires sync conflict rules.
- Requires careful transaction boundaries for checkout and recovery flows.
