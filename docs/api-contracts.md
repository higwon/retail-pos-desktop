# API Contracts

## General Rules

- Base route: `/api`.
- Use DTOs, not EF entities, as API contracts.
- Validate requests at the API boundary.
- Return RFC 7807-style ProblemDetails for unhandled errors and status-code errors.
- Hide technical exception details outside development.
- Log requests with method, path, status code, and elapsed time.
- Timestamp fields use UTC ISO 8601 values.
- Store-local reporting uses `businessDate`.
- Monetary values use whole-KRW `decimal` values.

## Health

`GET /api/health`

Response:

```json
{
  "status": "Healthy",
  "serverTime": "2026-07-02T01:00:00Z"
}
```

## Product Sync

`GET /api/products`

Query parameters:

- `updatedAfter`: optional UTC timestamp for incremental sync.
- `page`: 1-based page number, default `1`.
- `pageSize`: default `100`, maximum `500`.

Response shape:

```json
{
  "products": [
    {
      "id": "guid",
      "sku": "SKU-001",
      "barcode": "880000000001",
      "name": "Sample Product",
      "categoryName": "Beverage",
      "unitPrice": 1800,
      "stockQuantity": 10,
      "isActive": true,
      "version": 12,
      "updatedUtc": "2026-07-08T00:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 100,
  "hasMore": false,
  "serverTimeUtc": "2026-07-08T00:00:01Z"
}
```

Rules:

- Product/category data is sourced from upstream HQ/API in production.
- Desktop SQLite treats products as a local cache.
- `version` and `updatedUtc` are required for later incremental sync.
- Deleted or discontinued products are represented with `isActive: false`.
- Physical deletion is avoided in the sync contract.

## Order Upload

`POST /api/orders`

Request shape:

```json
{
  "schemaVersion": 1,
  "storeId": "guid",
  "terminalId": "guid",
  "localOrderId": "guid",
  "idempotencyKey": "store:guid:terminal:guid:localOrder:guid",
  "localOrderNumber": "POS-20260702-000001",
  "businessDate": "2026-07-02",
  "cashierId": "guid",
  "subtotalAmount": 10000,
  "discountAmount": 1000,
  "totalAmount": 9000,
  "createdAt": "2026-07-02T01:00:00Z",
  "lines": [
    {
      "productId": "guid",
      "productNameSnapshot": "Sample Product",
      "unitPrice": 5000,
      "quantity": 2,
      "lineDiscountAmount": 1000,
      "lineTotalAmount": 9000
    }
  ],
  "payments": [
    {
      "paymentMethod": "Card",
      "approvedAmount": 9000,
      "approvalCode": "APPROVED-001",
      "transactionReference": "TX-001",
      "approvedAtUtc": "2026-07-02T01:01:00Z"
    }
  ]
}
```

Response shape:

```json
{
  "serverOrderId": "guid",
  "orderNumber": "SYNCED-POS-20260702-000001",
  "syncStatus": "Synced"
}
```

Rules:

- `schemaVersion` is `1` for the MVP.
- `storeId`, `terminalId`, `localOrderId`, `idempotencyKey`, and `businessDate` are required.
- `createdAt` and payment `approvedAtUtc` are required and must be UTC timestamps.
- Line totals must match order total.
- Approved payment totals must match order total.
- Idempotency identity is `storeId + terminalId + localOrderId`.
- The same idempotency key for the same identity returns the existing result.
- Same key with a different identity returns `409 Conflict`.
- Same identity with a different key returns `409 Conflict`.
- When durable server order and stock persistence exists, server stock is deducted only on first creation.

## Future Batch Sync

`POST /api/sync/orders` is reserved for future batch upload. MVP order sync uses single-order `POST /api/orders`.
