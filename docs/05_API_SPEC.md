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

Request:

```json
{
  "idempotencyKey": "client-generated-guid",
  "localOrderNumber": "POS-20260702-000001",
  "cashierId": "guid",
  "subtotalAmount": 10000,
  "discountAmount": 1000,
  "totalAmount": 9000,
  "createdAt": "2026-07-02T10:00:00+09:00",
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
  "serverTime": "2026-07-02T10:00:00+09:00"
}
```

## API Design Rules

- Use RESTful routes.
- Use DTOs, not EF entities, as API contracts.
- Validate requests.
- Return clear error responses.
- Use pagination for list endpoints.
- Use idempotency for order upload.
