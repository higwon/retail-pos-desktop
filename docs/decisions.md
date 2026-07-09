# Decisions

This file is the source of truth for accepted project decisions. It uses an ADR-lite format in one file to avoid a large number of small decision documents.

## DEC-001 Use WPF for the Windows POS Client

Status: Accepted

### Context

The project targets Windows desktop POS workflows and should demonstrate maintainable native desktop engineering.

### Decision

Use WPF with MVVM for the desktop client.

### Reason

WPF fits a Windows-first POS portfolio project and supports mature MVVM patterns.

### Trade-offs

The desktop client is Windows-focused and does not target cross-platform UI.

### Consequences

The UI can support Windows-specific desktop behavior and simulator windows, but the client remains Windows-focused.

## DEC-002 Use SQLite for Local Offline Storage

Status: Accepted

### Context

The app must keep selling while the API is unavailable and must recover local operational data after restart.

### Decision

Use local SQLite for product cache, orders, pending checkouts, payments, and sync queue state.

### Reason

SQLite provides durable local storage with low operational overhead for offline-first checkout.

### Trade-offs

The client must handle later synchronization and conflict-safe upload behavior.

### Consequences

The app has a durable local store and can operate offline. Sync code must reconcile local data with the server later.

## DEC-003 Use EF Core for Local SQLite Persistence

Status: Accepted

### Context

Local persistence needs migrations, mapping, and repository implementations without excessive boilerplate.

### Decision

Use EF Core with SQLite for local persistence.

### Reason

EF Core gives migrations, mapping, and testable repository implementations without excessive hand-written SQL.

### Trade-offs

Persistence code must keep EF Core concerns inside Infrastructure.

### Consequences

Migrations and mappings are maintained in Infrastructure. Application and Domain remain independent of EF Core.

## DEC-004 Store PendingCheckout Before Payment Approval

Status: Accepted

### Context

Payment approval may succeed while local order creation fails or the app terminates.

### Decision

Create and persist `PendingCheckout` before requesting payment approval. After approval, save the order and sync queue item before marking the pending checkout completed.

### Reason

Payment approval can succeed before local order creation is durable, so the approved state must be recoverable.

### Trade-offs

Checkout has an extra persistence state and recovery flow to maintain.

### Consequences

Startup can detect approved-but-not-created checkout state and route to recovery. Recovery must be idempotent.

## DEC-005 Cached Employee Login for Offline MVP

Status: Accepted

### Context

Stores may lose network access, but cashiers need to keep operating.

### Decision

The first login on a terminal requires online authentication. Later offline login is allowed only for cached employees for 7 days after the last successful online login. Permissions use the most recent synchronized role/permission snapshot.

### Reason

Cashiers need limited offline continuity without allowing indefinite stale access.

### Trade-offs

Terminals need a secure employee cache and must reject expired offline credentials.

### Consequences

Offline login is possible but bounded. Missing or expired cache must deny offline login.

## DEC-006 Server Stock Is Authoritative

Status: Accepted

### Context

Local orders can exist before server sync, so local stock can only be an estimate.

### Decision

Server stock is authoritative. Local stock is an estimated display value calculated from synchronized server stock minus pending local deductions.

### Reason

Multiple terminals can sell while offline, so only the server can be the final stock source of truth.

### Trade-offs

Local stock may be approximate until sync completes and product stock refreshes.

### Consequences

Product sync must not overwrite pending local deductions. Unsynced local sales appear as pending deductions until server sync completes and product stock is refreshed.

## DEC-007 Order Upload Identity and Idempotency

Status: Accepted

### Context

Order upload can retry after network failures and must not create duplicate server orders.

### Decision

Order upload requires `storeId`, `terminalId`, `localOrderId`, `idempotencyKey`, and `businessDate`. The idempotency identity is `storeId + terminalId + localOrderId`. A stable `idempotencyKey` is reused for every retry of that identity.

### Reason

Network retries must be duplicate-safe and operationally traceable by store, terminal, and local order.

### Trade-offs

The client must persist stable upload identity data and treat identity conflicts as manual-review cases.

### Consequences

Duplicate retries return the existing result. Same key with different identity, or same identity with a different key, returns conflict and moves toward manual review.

## DEC-008 MVP Discounts Are Manual Only

Status: Accepted

### Context

Discount rules can become complex and distract from the POS MVP.

### Decision

MVP supports only manual fixed amount and manual percentage discounts.

### Reason

Manual discounts cover the MVP cashier flow without pulling in complex promotion rules too early.

### Trade-offs

Coupons, promotions, memberships, and rule-engine scenarios wait until Phase 2.

### Consequences

Coupon, promotion, membership, and discount rule engine work is Phase 2.

## DEC-009 Money, Currency, Time, and Retry Rules

Status: Accepted

### Decision

- Money uses .NET `decimal`.
- MVP currency is KRW.
- Amounts are rounded to whole won with no fractional display.
- Persist timestamps in UTC.
- Display timestamps in local time.
- Sync retry uses exponential backoff.
- Maximum automatic retry attempts: 5.
- MVP retry delays: 1, 2, 4, 8, and 16 minutes.
- After 5 failed attempts, mark the item exhausted for manual review or retry.
- Refund and cancellation are excluded from MVP and planned for Phase 2.

### Reason

These defaults keep money, time, and retry behavior predictable across Desktop, API, and tests.

### Trade-offs

Some operational edge cases are deferred to Phase 2 or hardening tasks instead of expanding the MVP.

## DEC-010 API Skeleton Includes Operational Basics

Status: Accepted

### Decision

The API skeleton includes health endpoint, ProblemDetails, global exception handling, request logging, and production-safe error responses.

### Reason

The API should start with basic operability and safe error behavior instead of being only a placeholder endpoint.

### Trade-offs

The skeleton includes a little more infrastructure up front, but it stays tied to immediately useful API behavior.

### Consequences

The API starts with operational visibility instead of only a placeholder health endpoint.
