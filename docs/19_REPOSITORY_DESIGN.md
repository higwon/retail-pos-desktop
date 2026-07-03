# Repository Design

This document defines repository contracts for local persistence.

## Goal

Repository interfaces should express application use cases without leaking SQLite, EF Core, or WPF details.

## Ownership

```text
RetailPOS.Application
    owns interfaces

RetailPOS.Infrastructure
    owns implementations
```

## Recommended Interfaces

### IProductRepository

Purpose:

- Read local product catalog data.
- Support future product search and barcode lookup.

Expected responsibilities:

- Get active products
- Get product by ID
- Get product by barcode
- Search products by keyword

### IOrderRepository

Purpose:

- Store completed local orders.
- Read completed orders for later upload/status views.

Expected responsibilities:

- Save completed order
- Get order by local ID or local order number
- Get recent orders
- Check whether an order already exists by idempotency/local key if needed

### IPendingCheckoutRepository

Purpose:

- Store recoverable checkout state.
- Restore interrupted checkout after app restart.

Expected responsibilities:

- Save pending checkout
- Get unresolved pending checkouts
- Mark checkout as completed/resolved
- Delete or archive resolved checkout records

### ISyncQueueRepository

Purpose:

- Track local records that need later upload/status processing.

Expected responsibilities:

- Enqueue item
- Get pending items
- Update retry metadata
- Mark item completed/resolved

## Method Design Rules

- Prefer async methods for local I/O boundaries.
- Use domain models or application-level DTOs only.
- Do not expose EF Core `DbContext`, `DbSet`, `IQueryable`, or SQLite-specific types.
- Do not put UI concepts in repository contracts.

## Testing Direction

- Application interface tests are not required.
- Infrastructure repository tests should verify save/read/update behavior when concrete implementations are added.
