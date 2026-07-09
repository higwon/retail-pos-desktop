# Database Design

## Database Strategy

Use two database contexts:

1. Local SQLite database for the WPF desktop client.
2. Central SQL Server database for the API server.

The local database exists to support offline-first operation.
The central database exists to represent synchronized store/server data.

## Local SQLite Database

### Main Tables

#### Employees

- Id
- EmployeeCode
- DisplayName
- Role
- PasswordHash or local auth token placeholder for development
- IsActive
- LastOnlineAuthenticatedAt
- PermissionsSyncedAt

#### Products

- Id
- Sku
- Barcode
- Name
- CategoryName
- UnitPrice
- StockQuantity
- IsActive
- Version
- UpdatedUtc

#### Categories (Later)

- Id
- Name
- SortOrder

EPIC-04 keeps `CategoryName` on `Products`. A normalized category table is deferred
until category management is implemented.

#### Orders

- Id
- StoreId
- TerminalId
- LocalOrderId
- LocalOrderNumber
- ServerOrderId nullable
- CashierId
- OrderStatus
- PaymentStatus
- SyncStatus
- IdempotencyKey
- BusinessDate
- SubtotalAmount
- DiscountAmount
- TotalAmount
- CreatedAt
- SyncedAt nullable

#### PendingCheckouts

- Id
- StoreId
- TerminalId
- LocalCheckoutId
- CashierId
- CartSnapshotJson
- PaymentMethod
- PaymentStatus
- ApprovalCode nullable
- ApprovedAmount nullable
- TransactionReference nullable
- RecoveryStatus
- CreatedAt
- PaymentApprovedAt nullable
- OrderId nullable
- CompletedAt nullable

`PendingCheckout` is saved before payment approval is requested. An approved checkout remains in `ApprovedButOrderNotCreated` until its order is durably saved. This record supports recovery after application or machine restart.

#### OrderLines

- Id
- OrderId
- ProductId
- ProductNameSnapshot
- UnitPrice
- Quantity
- LineDiscountAmount
- LineTotalAmount

#### Payments

- Id
- OrderId
- PaymentMethod
- PaymentStatus
- ApprovedAmount
- ApprovalCode nullable
- FailureReason nullable
- CreatedAt

#### SyncQueue

- Id
- EntityType
- EntityId
- OperationType
- PayloadJson
- Status
- RetryCount
- LastError
- CreatedAt
- LastAttemptedAt nullable

For order upload records, `PayloadJson` stores the complete API upload payload shape, including `schemaVersion`, order identity, order number, cashier, business date, UTC timestamps, line items, totals, approved payments, and idempotency key.

Automatic retries use exponential backoff and stop after 5 attempts. Exhausted items remain available for manual review or retry.
`Exhausted` queue records are no longer selected by automatic due-pending sync queries.

#### DeviceLogs

- Id
- DeviceType
- Message
- Level
- CreatedAt

## Server SQL Server Database

The server database can start with a similar structure, but should not depend on local-only fields unless necessary.

Server-side tables:

- Stores
- Employees
- Products
- Categories
- Orders
- OrderLines
- Payments
- SyncRequests

## Sync Status Values

Recommended enum:

- Pending
- InProgress
- Synced
- Failed
- Conflict

## Order Status Values

Recommended enum:

- Draft
- Completed
- Cancelled
- Refunded

`Cancelled` and `Refunded` are reserved for Product Phase 2 and are not implemented in the MVP workflow.

## Payment Status Values

Recommended enum:

- Pending
- Approved
- Failed
- Cancelled

## Idempotency

Every locally completed order should have an `IdempotencyKey`.

The API should use this key to prevent duplicate order creation if the client retries after a timeout.

For order uploads, the idempotency identity is the tuple `StoreId + TerminalId + LocalOrderId`. The transmitted `IdempotencyKey` represents this identity and is protected by a unique server-side constraint.

## Stock Projection

- Server stock is authoritative.
- `StockQuantity` stores the last synchronized server value until pending local stock deduction is implemented.
- `ServerStockQuantity` may be introduced later if local estimated stock needs a separate persisted column.
- Pending deduction is the total sold quantity in locally completed orders that have not synchronized successfully.
- Local estimated stock is calculated as `StockQuantity - PendingDeductionQuantity` in the current model.
- A product refresh updates the server value and then reapplies pending deductions instead of overwriting local estimates directly.

## Data Type Rules

- Monetary columns use a decimal-compatible mapping and contain whole KRW amounts.
- Fractional won values are not stored.
- Timestamps are persisted in UTC.
- `BusinessDate` is the store-local calendar date used for reporting and is not a replacement for UTC timestamps.

## Initial Migration Rule

Do not over-model the database at the beginning.
Start with the MVP tables, then evolve through migrations.
