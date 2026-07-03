# Persistence Flow

This document defines the local persistence flow for checkout, recovery, and later synchronization.

## Core Checkout Persistence Flow

```text
Cart ready
    -> Start checkout
    -> Save PendingCheckout
    -> Payment approved
    -> Save Completed Order
    -> Mark PendingCheckout resolved
    -> Enqueue Order Upload record
```

## Why PendingCheckout Exists

The dangerous failure window is:

```text
payment approved -> app exits before order save
```

To avoid losing an approved payment, the application must have a recoverable local record before or during payment completion.

## Recovery Flow

```text
App starts
    -> Read unresolved PendingCheckout records
    -> Show recovery screen if needed
    -> User reviews recovery record
    -> Complete order or mark resolved according to later business rules
```

EPIC-04 only stores and reads the records. Real recovery behavior belongs to later checkout tasks.

## Order Save Flow

```text
Domain Order
    -> Infrastructure mapper
    -> OrderEntity
    -> OrderLineEntity
    -> PaymentEntity
    -> SQLite transaction
```

Order save should be atomic when possible.

## Sync Queue Flow

```text
Completed order saved locally
    -> SyncQueue item created
    -> Later worker/API task uploads item
    -> Queue item marked completed or retry metadata updated
```

EPIC-04 only persists queue/status data. Real network upload and retry worker behavior are later tasks.

## Transaction Boundary Direction

For later checkout implementation, the ideal local transaction is:

```text
Save completed order
Mark pending checkout resolved
Create sync queue record
```

These should eventually happen in one local transaction if practical.

## Time Rules

- Persist UTC timestamps.
- Keep business date separate from UTC timestamp.
- Do not store ambiguous local time as the only source of truth.

## Money Rules

- Use `decimal` in C#.
- Use whole-KRW values.
- Do not use floating-point types for money.
