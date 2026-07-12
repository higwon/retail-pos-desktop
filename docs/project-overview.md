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

- Demo cashier and manager login with current terminal session context.
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

## Current Implementation and Target Policies

The current portfolio implementation and the accepted production-oriented policies are
not always at the same delivery stage.

- Current login uses deterministic demo accounts and does not persist an employee
  authentication cache.
- Cached employee login, seven-day offline validity, and synchronized permission
  snapshots remain the accepted target policy.
- Current product sync persists server stock quantities in the local cache.
- Pending local-order stock deduction and estimated local stock remain target behavior
  for later hardening.
- Device simulation and production-readiness hardening are implemented for the portfolio
  build. Production hardware adapters, identity, deployment, and advanced multi-display
  administration remain Phase 2.

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
