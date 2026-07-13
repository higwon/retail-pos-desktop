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

Implementation status: Target policy. The current portfolio build uses deterministic
demo accounts and does not persist an employee authentication cache.

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

Implementation status: Partially implemented. Product sync persists server stock
quantities; pending local deductions and estimated local stock are not yet calculated.

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

## DEC-011 Separate Device Business Ports from Simulator Controls

Status: Accepted

### Context

EPIC-08 introduces barcode scanner, receipt printer, card terminal, and customer
display simulators. Existing boundaries are uneven: receipt printing already has a
business-facing port, while payment requests currently carry simulator scenario
selection and barcode/customer-display ownership is UI-specific.

### Decision

Application device ports expose normal business operations only. Simulator scenario,
delay, connection, and failure controls are separate contracts implemented outside
Application business services.

Device simulators live in Infrastructure unless they directly own Windows or WPF
resources, in which case the host behavior stays in Desktop. Terminal-owned devices
have one instance per terminal UI scope. Long-running operations accept cancellation,
and Desktop owns WPF dispatcher transitions for callbacks raised off the UI thread.

Shared connection vocabulary is limited to `Disconnected`, `Connecting`,
`Connected`, and `Faulted`. Operational states remain device-specific until real
duplication justifies another abstraction.

Barcode simulation uses an event-producing scanner boundary while preserving
keyboard-wedge/manual input as a fallback. Customer-display data remains separate from
the Desktop host that discovers monitors and owns the display window.

Payment outcomes are fail-closed:

- An approved response is `Approved`.
- A declined response is `Failed`.
- Cancellation is `Cancelled` only when it occurs before dispatch or is confirmed by
  the terminal.
- Timeout, communication loss, or unconfirmed cancellation after dispatch is
  `Unknown`.
- `Unknown` must not create an order, clear the cart, or allow silent immediate
  retry. It must remain discoverable for review or reconciliation.
- An interrupted persisted `AwaitingPayment` becomes `Unknown` manager review on
  recovery. Explicit manager resolution preserves the record and releases the
  terminal for a new payment attempt.

### Reason

Business workflows should remain valid when a simulator is replaced by a hardware
adapter. Separating control surfaces prevents scenario configuration from leaking into
checkout use cases and keeps delayed or indeterminate device behavior recoverable.

### Trade-offs

Simulator implementations require a separate control contract, and device-specific
state types may initially repeat a small amount of structure.

### Consequences

EPIC-08 implementations must keep scenario selection out of cashier business commands,
propagate cancellation, define device lifetime explicitly, and preserve Unknown
payment outcomes for review. A generic device framework is deferred until concrete
implementations demonstrate a stable shared shape.

## DEC-012 Use Typed In-Window Navigation for Cashier Workflows

Status: Accepted

### Context

Payment and receipt are currently separate workflow windows, while product search,
recovery, and operational screens use a mixture of direct view replacement and window
hosts. EPIC-10 needs deterministic screen transitions without coupling ViewModels to
WPF views or creating more View event bridges.

### Decision

Desktop owns a scoped typed workflow navigator with explicit screen identifiers,
transition policy, back history, and push, replace, back, and reset semantics.
`NavigationHost` is the thin WPF bridge that maps supported states to views.

NavigationHost registers its screen-to-view map with a scoped screen registry. The
navigator validates that a destination is registered before committing state. A new
screen's View registration and first transition path therefore ship together. The
post-commit `ScreenChanged` event is notification-only and does not provide rollback.

Root reset is limited to Login, Register, Receipt History, Recovery, Dashboard, and
Status. Product Search, Card Payment, Cash Payment, and Receipt Detail must be entered
through valid workflow transitions. Invalid and undefined transitions fail without
changing navigation state, and duplicate transitions are no-ops.

The navigator does not authenticate or authorize users. Session access remains owned by
`ICurrentSessionContext` and the signed-in shell; reset controls history and root-screen
selection after that access decision.

Device Simulator remains a modeless operator utility window. Customer Display remains
a dedicated device-output window. Existing Payment and Receipt windows remain only
until their in-window replacements are complete.

### Reason

One typed navigation path makes back, cancel, completion, recovery, and sign-out
behavior testable without ViewModel-to-View references. It also allows the UI migration
to ship in focused PRs while preserving current checkout behavior.

### Trade-offs

The navigator initially contains screen states whose views arrive in later tasks, and
`NavigationHost` must be updated whenever a new screen implementation becomes active.
