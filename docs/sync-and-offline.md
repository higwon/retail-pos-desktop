# Sync and Offline

## Offline Login

- First login on a terminal requires the API.
- After a successful online login, the app stores the minimum employee authentication data required for offline login.
- Offline login is allowed only for cached employees.
- Offline login is valid for 7 days from the most recent successful online authentication.
- Offline permissions use the most recent synchronized role and permission snapshot.
- Missing or expired cached credentials must deny offline login.

## Local Database

Default local database path:

```text
%LOCALAPPDATA%\RetailPOS\retail-pos.db
```

The path can be overridden with `LocalDatabase:DatabasePath`.

Local SQLite stores:

- Product cache.
- Orders.
- Order lines.
- Payments.
- Pending checkouts.
- Sync queue items.
- Product sync metadata.

## Checkout Recovery

`PendingCheckout` is persisted before payment approval.

```text
Cart confirmed
-> PendingCheckout created
-> Payment approval requested
-> Approval result stored
-> Order created locally
-> SyncQueue item created
-> PendingCheckout completed
```

On startup, if a pending checkout is in `ApprovedButOrderNotCreated`, the user is routed to recovery. Recovery recreates the order idempotently or leaves the item for manager review.

## Stock Rules

- Server stock is authoritative.
- Local stock is an estimated display value.
- Unsynced local orders are pending deductions.
- Local estimated stock equals synchronized server stock minus pending deduction quantities.
- Product sync must not overwrite pending local deductions.
- After order sync succeeds, the server deducts stock and the client later refreshes product stock.

## Sync Queue

Local orders are queued after order completion. The sync worker uploads due order queue items to the API.

Status expectations:

- `Pending`: not yet uploaded or ready for retry.
- `Completed`: successfully synced.
- `Exhausted`: automatic retries stopped and manual review is needed.

Retry rules:

- Automatic retry limit: 5 attempts.
- Retry delays: 1, 2, 4, 8, and 16 minutes.
- After the fifth failed attempt, do not schedule another automatic retry.
- Idempotency conflict is non-retryable and should move toward manual review.

## Background Sync

Desktop has:

- `BackgroundOrderSyncScheduler` for periodic order sync.
- `ApiConnectivityMonitor` for API health checks.
- Reconnect-triggered sync when connectivity changes back to online.
- Messenger-based sync status refresh for the Status screen.

Configuration lives in `src/RetailPOS.Desktop/appsettings.json`:

- `ApiSync:BaseAddress`
- `SyncScheduler:Enabled`
- `SyncScheduler:InitialDelaySeconds`
- `SyncScheduler:IntervalSeconds`
- `SyncScheduler:BatchSize`
- `ApiConnectivity:Enabled`
- `ApiConnectivity:InitialDelaySeconds`
- `ApiConnectivity:IntervalSeconds`
- `ApiConnectivity:TriggerSyncOnReconnect`

## Logging

Desktop logs use Serilog and default to:

```text
%LOCALAPPDATA%\RetailPOS\logs
```

Sync logs should include enough safe context to trace store, terminal, local order, order number, status, attempt count, and elapsed time where available.

Do not log raw idempotency keys, approval codes, transaction references, tokens, secrets, passwords, or payment-sensitive values.
