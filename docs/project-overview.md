# Project Overview

## Goal

Retail POS Desktop is a portfolio-oriented Windows POS system that demonstrates practical desktop application engineering for retail workflows.

The project emphasizes:

- Offline-first sales processing.
- Local persistence and recovery.
- Server synchronization.
- Clean WPF/MVVM structure.
- Realistic POS concepts such as carts, payments, receipts, stock, and device simulation.

## Main Users

### Cashier

- Logs in.
- Searches or scans products.
- Manages the cart.
- Applies MVP manual discounts.
- Completes payment.
- Shows receipt and customer-facing display state.

### Store Manager

- Reviews failed or exhausted sync items.
- Handles checkout recovery cases.
- Reviews sales and sync status.

### System Administrator

- Operates the central API.
- Monitors health and logs.
- Supports product and order synchronization.

## MVP Features

- Cached employee login for offline mode after a successful online login.
- Product lookup from local SQLite.
- Cart operations.
- Manual amount and manual rate discounts.
- Cash/card payment simulator.
- Recoverable checkout through `PendingCheckout`.
- Local order persistence.
- Receipt preview.
- Customer display simulator.
- Order upload sync with retry and idempotency.
- API connectivity monitor.
- Background sync scheduler.
- Sync status UI refresh through messenger notifications.

## MVP Exclusions

- Refund workflow.
- Order cancellation workflow.
- Coupon, promotion, membership, or rule-engine discounts.
- Real payment terminal integration.
- Real receipt printer integration.
- Server-side durable stock persistence beyond the current API skeleton.

## Core Checkout Flow

```text
Cart confirmed
-> PendingCheckout created locally
-> Payment approval requested
-> Payment approved
-> Order saved locally
-> SyncQueue item created
-> PendingCheckout completed
-> Receipt/customer display updated
-> Background sync uploads order later
```

If the app restarts with an approved payment that did not finish order creation, checkout recovery must show that state and allow idempotent recovery or manager review.
