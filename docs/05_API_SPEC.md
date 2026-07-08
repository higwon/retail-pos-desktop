# API Specification

## API Goal

The API represents the central server used by POS clients.

It should support:

- Authentication
- Product synchronization
- Order upload
- Sales history
- Stock updates
- Health checks

## Base Route

```text
/api
```

## Authentication

### POST /api/auth/login

Request:

```json
{
  "employeeCode": "E0001",
  "password": "password"
}
```

Response:

```json
{
  "accessToken": "jwt-token",
  "employeeId": "guid",
  "displayName": "Cashier A",
  "role": "Cashier"
}
```

## Products

### GET /api/products

Returns product list for synchronization.

Query parameters:

- `updatedAfter`: optional UTC timestamp for incremental sync.
- `page`: 1-based page number. Default is `1`.
- `pageSize`: number of items per page. Default is `100`, maximum is `500`.

Response:

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

Required behavior:

- Product and category data is sourced from the upstream HQ/API flow in production.
- Desktop SQLite treats products as a local cache for offline lookup.
- `version` and `updatedUtc` must be included so Desktop can later perform incremental sync.
- Deleted or discontinued products should be returned as `isActive: false` instead of being physically removed from the contract.
- Monetary values use whole-KRW `decimal` values.

### GET /api/products/{id}

Returns one product.

### GET /api/products/search

Query parameters:

- `keyword`
- `barcode`
- `sku`

## Orders

### POST /api/orders

Uploads a completed local order.

Required behavior:

- `schemaVersion` must be `1` for the MVP upload contract.
- `storeId`, `terminalId`, `localOrderId`, `idempotencyKey`, and `businessDate` are required.
- `createdAt` and payment `approvedAtUtc` must be UTC timestamps.
- Monetary values use whole-KRW `decimal` values.
- Line totals must match the order total.
- Approved payment totals must match the order total.
- POS-504 validates the contract and may return a placeholder `Accepted` status before persistence exists.
- POS-505 implements durable persistence, idempotency lookup, and final duplicate-safe `Synced` behavior.
- Must support idempotency.
- If the same idempotency key already exists, return the existing server order result.
- Treat `storeId + terminalId + localOrderId` as the order's idempotency identity.
- Generate one stable `idempotencyKey` for each `storeId + terminalId + localOrderId` identity and reuse it for every retry.
- Deduct server stock only when the server order is created for the first time.
- A duplicate request must return the existing result without deducting stock again.

Request:

```json
{
  "schemaVersion": 1,
  "storeId": "guid",
  "terminalId": "guid",
  "localOrderId": "guid",
  "idempotencyKey": "client-generated-guid",
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

Response:

```json
{
  "serverOrderId": "guid",
  "orderNumber": "PENDING-POS-20260702-000001",
  "syncStatus": "Accepted"
}
```

### GET /api/orders

Returns server order history.

Query parameters:

- `from`
- `to`
- `cashierId`
- `page`
- `pageSize`

## Sync

### POST /api/sync/orders

Batch upload endpoint for pending local orders.

Request:

```json
{
  "orders": []
}
```

Response:

```json
{
  "results": [
    {
      "idempotencyKey": "client-generated-guid",
      "serverOrderId": "guid",
      "status": "Synced",
      "errorMessage": null
    }
  ]
}
```

## Health

### GET /api/health

Returns server health status.

Response:

```json
{
  "status": "Healthy",
  "serverTime": "2026-07-02T01:00:00Z"
}
```

## API Design Rules

- Use RESTful routes.
- Use DTOs, not EF entities, as API contracts.
- Validate requests.
- Return clear error responses.
- Return RFC 7807-style ProblemDetails responses for unhandled server errors and status-code errors.
- Log API requests with method, path, status code, and elapsed time.
- Keep technical exception details out of production error responses.
- Use pagination for list endpoints.
- Use idempotency for order upload.
- Timestamp fields in API contracts use UTC ISO 8601 values. Store-local reporting dates use `businessDate`.
