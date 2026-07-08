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

- `updatedAfter`
- `page`
- `pageSize`

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

- Must support idempotency.
- If the same idempotency key already exists, return the existing server order result.
- Treat `storeId + terminalId + localOrderId` as the order's idempotency identity.
- Generate one stable `idempotencyKey` for each `storeId + terminalId + localOrderId` identity and reuse it for every retry.
- Deduct server stock only when the server order is created for the first time.
- A duplicate request must return the existing result without deducting stock again.

Request:

```json
{
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
      "approvalCode": "APPROVED-001"
    }
  ]
}
```

Response:

```json
{
  "serverOrderId": "guid",
  "orderNumber": "S-20260702-000001",
  "syncStatus": "Synced"
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
