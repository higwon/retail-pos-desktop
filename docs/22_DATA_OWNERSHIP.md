# Data Ownership and Source of Truth

This document defines where major data types originate, which layer owns them, and how local SQLite should be treated after EPIC-05.

## Guiding Rules

- Local SQLite is not a single source of truth for every data type.
- Some data is an upstream cache, and some data is locally created operational state.
- Development seed data exists only to make the desktop app usable before API sync is implemented.
- Production product and category data must come from the upstream HQ/API flow.
- Completed local orders are created on the desktop first, then uploaded through the sync queue.

## Ownership Matrix

| Data type | Production source of truth | Local SQLite role | Local origin allowed | Notes |
| --- | --- | --- | --- | --- |
| Products | Upstream HQ/API | Offline read cache | No, except development seed | Product sync upserts the local cache. Server data wins. |
| Categories | Upstream HQ/API | Offline read cache | No, except development seed | Category seed data is demo-only until category sync exists. |
| Orders | Desktop POS at checkout time, then central API after upload | Local operational record and upload source | Yes | The desktop creates local orders first so offline sales can continue. |
| PendingCheckout | Desktop checkout flow | Local operational recovery record | Yes | Exists to recover approved payments that did not finish order creation. |
| Receipts | Derived from completed local orders | Generated output derived from order data | Yes, derived only | MVP receipts are not a separate source of truth. Reprint history can be added later. |
| SyncQueue | Desktop sync boundary | Local operational queue | Yes | Tracks upload payload, status, retry count, and failure reason. |
| Store | Future API/config | Local configuration/cache | Temporarily demo/config | Production ownership should move to API/config. |
| Terminal | Future API/config | Local configuration/cache | Temporarily demo/config | Terminal identity is required for idempotency and audit. |
| Cashier | Future auth/API | Local authenticated user/cache | Cached login only | Offline login may use cached employee data within the documented 7-day MVP rule. |
| BusinessDate | Store operating policy from future API/config | Stored on local orders and upload DTOs | Derived locally until policy sync exists | Keep business date separate from UTC timestamps. |

## Development Seed Versus Production Sync

Seeded products and categories are development/demo data only. They keep the desktop UI testable before EPIC-06 product sync is complete.

Production flow:

```text
HQ/API product catalog
    -> Product sync endpoint
    -> Desktop sync client
    -> Local SQLite product/category cache
    -> POS search and cart UI
```

When upstream product or category data changes, the local cache should be updated by sync. If an upstream product is discontinued or deleted, the preferred MVP policy is a soft delete/inactive flag in the local cache rather than physical deletion, so historical orders and receipts remain readable.

## Local Operational Data

The desktop owns local operational data created during checkout:

- `PendingCheckout` is saved before payment approval.
- `Order` is saved locally after payment approval.
- `Receipt` is generated from the completed local order.
- `SyncQueue` is created with the order upload payload and retry metadata.

The expected flow is:

```text
Cart confirmed
    -> PendingCheckout saved locally
    -> Payment approved
    -> Order saved locally
    -> Receipt generated from order
    -> SyncQueue upload record created
    -> PendingCheckout completed
```

The API does not create the first local order for offline checkout. It receives uploaded orders later and must treat the upload idempotently.

## Receipt Ownership

MVP receipt data is derived from completed local orders and payment data. A receipt preview or print result must not become a separate business truth that can disagree with the order.

If reprint history, receipt numbering, or fiscal receipt storage is added later, it should be introduced as a separate documented requirement and schema change.

## Sync and Idempotency Notes

Order upload payloads must include:

- `storeId`
- `terminalId`
- `localOrderId`
- `idempotencyKey`
- `businessDate`
- `schemaVersion`

The idempotency key scope is:

```text
storeId + terminalId + localOrderId
```

The API should also log the human-facing order number/reference where available, so operations can trace duplicate or retried uploads.

## Related Documents

- [Architecture](03_ARCHITECTURE.md)
- [Local Persistence Architecture](18_LOCAL_PERSISTENCE.md)
- [Repository Design](19_REPOSITORY_DESIGN.md)
- [Persistence Flow](21_PERSISTENCE_FLOW.md)
- [Roadmap](08_ROADMAP.md)
