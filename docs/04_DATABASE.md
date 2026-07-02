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

#### Products

- Id
- Sku
- Barcode
- Name
- CategoryId
- UnitPrice
- StockQuantity
- IsActive
- UpdatedAt

#### Categories

- Id
- Name
- SortOrder

#### Orders

- Id
- LocalOrderNumber
- ServerOrderId nullable
- CashierId
- OrderStatus
- PaymentStatus
- SyncStatus
- IdempotencyKey
- SubtotalAmount
- DiscountAmount
- TotalAmount
- CreatedAt
- SyncedAt nullable

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

## Payment Status Values

Recommended enum:

- Pending
- Approved
- Failed
- Cancelled

## Idempotency

Every locally completed order should have an `IdempotencyKey`.

The API should use this key to prevent duplicate order creation if the client retries after a timeout.

## Initial Migration Rule

Do not over-model the database at the beginning.
Start with the MVP tables, then evolve through migrations.
