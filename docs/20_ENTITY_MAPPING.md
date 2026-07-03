# Entity Mapping

This document defines how Domain models should map to local SQLite persistence models.

## Goal

Domain models should stay clean and persistence-agnostic. SQLite entities may be shaped for storage and querying.

## Rule

Do not force Domain models to become database entities.

```text
Domain Model <-> Mapper <-> SQLite Entity
```

## Product Mapping

```text
Product
    -> ProductEntity
```

Suggested stored fields:

- Id
- Sku
- Barcode
- Name
- CategoryName
- UnitPrice
- IsActive
- UpdatedAtUtc if needed later

EPIC-04 stores `CategoryName` directly because the current Domain model uses a category
name and the MVP does not manage categories independently. A normalized `Categories`
table and `CategoryId` may be introduced later with a migration when category management
becomes a real use case.

## Order Mapping

```text
Order
    -> OrderEntity
    -> OrderLineEntity
    -> PaymentEntity
```

Suggested order fields:

- LocalOrderId
- LocalOrderNumber
- StoreId
- TerminalId
- CashierId
- BusinessDate
- CreatedAtUtc
- Status
- SubtotalAmount
- DiscountAmount
- TotalAmount

Suggested order line fields:

- Id
- LocalOrderId
- ProductId
- ProductNameSnapshot
- UnitPrice
- Quantity
- GrossAmount
- LineDiscountAmount
- LineTotalAmount

Suggested payment fields:

- Id
- LocalOrderId
- Method
- Status
- RequestedAmount
- ApprovedAmount
- CreatedAtUtc
- ApprovedAtUtc
- ApprovalCode
- TransactionReference
- FailureReason

## Pending Checkout Mapping

```text
PendingCheckoutRecord
    -> PendingCheckoutEntity
```

Suggested stored fields:

- Id
- StoreId
- TerminalId
- CashierId
- CreatedAtUtc
- RecoveryStatus
- CartSnapshotJson
- PaymentSnapshotJson
- PaymentStatus
- ApprovalCode nullable
- ApprovedAmount nullable
- PaymentApprovedAtUtc nullable
- OrderId nullable
- CompletedAtUtc nullable
- LastUpdatedAtUtc

The snapshot may initially be JSON to keep recovery persistence simple. It can be normalized later if needed.

Recommended recovery status values:

- `AwaitingPayment`
- `PaymentFailed`
- `ApprovedButOrderNotCreated`
- `ManagerReviewRequired`
- `Completed`

`PaymentStatus`, approval fields, and recovery status are explicit columns so startup
recovery does not depend on querying JSON. The JSON snapshots preserve the data needed
to reconstruct the cart and payment context.

## Sync Queue Mapping

```text
SyncQueueRecord
    -> SyncQueueEntity
```

Suggested stored fields:

- Id
- ItemType
- AggregateId
- PayloadJson or ReferenceKey
- Status
- RetryCount
- NextAttemptAtUtc
- LastErrorSummary
- CreatedAtUtc
- UpdatedAtUtc

New queue records set `NextAttemptAtUtc` equal to `CreatedAtUtc`. Pending queries use
`NextAttemptAtUtc`, then `CreatedAtUtc`, then `Id` as a stable ascending sort order.

## Mapper Rules

- Keep mapper code in Infrastructure.
- Do not add persistence attributes to Domain models unless a later ADR explicitly allows it.
- Preserve whole-KRW decimal values.
- Preserve UTC timestamps.
