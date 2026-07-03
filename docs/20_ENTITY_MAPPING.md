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
- Status
- CartSnapshotJson
- PaymentSnapshotJson
- LastUpdatedAtUtc

The snapshot may initially be JSON to keep recovery persistence simple. It can be normalized later if needed.

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

## Mapper Rules

- Keep mapper code in Infrastructure.
- Do not add persistence attributes to Domain models unless a later ADR explicitly allows it.
- Preserve whole-KRW decimal values.
- Preserve UTC timestamps.
