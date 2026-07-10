# Roadmap

GitHub Issues are the detailed task tracker. This file keeps the higher-level phase map.

For the implementation scope, task IDs, acceptance criteria, and issue creation source, see [Epics and Tasks](epics-and-tasks.md).

## Completed Foundation

- Documentation seed.
- Solution and project structure.
- Project references and DI foundation.
- Main window and navigation shell.
- Base theme resources.
- Global desktop exception handling.
- Configuration foundation.
- Domain models for products, cart, order, payment, discounts, and receipt.
- Local SQLite persistence and repositories.
- Checkout recovery persistence.
- POS shell screens and placeholder flows.
- Customer display state and receipt preview.
- API skeleton with ProblemDetails and request logging.
- Product sync contract and Desktop client.
- Order upload contract and idempotency handling.
- Order sync worker and retry/exhausted behavior.
- Sync status UI.
- Sync integration tests.
- Structured desktop sync logging.
- Background sync scheduler.
- API connectivity monitor.
- Messenger-based sync status refresh.

## Completed POS Workflow

EPIC-07 completed the cashier-facing workflow:

- Demo login and current cashier/session context.
- Workflow-aware POS header and operational dashboard.
- Cart checkout as the primary payment entry point.
- Barcode fast path with text-search fallback.
- Hardened payment outcomes and a receipt-printer boundary.
- Deterministic cashier happy-path validation through SQLite order and sync-queue persistence.

## Completed Device Simulation

EPIC-08 demonstrates device integration concepts:

- Barcode scanner simulator.
- Receipt printer simulator.
- Card reader simulator.
- Secondary monitor customer display.

Simulator controls remain separate from cashier business commands. Real hardware
adapters remain Phase 2.

## Next Planned Area

Before EPIC-09, the EPIC-08 follow-up turns the simulator into a stronger interactive
integration demo and addresses visible cashier UX gaps:

- Operator-driven printer and card request/response workflows.
- Receipt payload and payment request inspection in the simulator.
- Product selection for simulated barcode scans.
- Cashier-facing device connectivity/readiness status.
- Receipt/payment feedback consistency and layout fixes.
- Product category filtering, clickable product tiles, header/navigation cleanup, and
  dashboard row sizing.

## Later Planned Areas

Production readiness follows after core workflow and device simulation:

- Configuration hardening.
- Logging/audit hardening.
- Offline and recovery scenario tests.
- Performance and reliability polish.

## Phase 2 Product Ideas

- Refunds.
- Order cancellation.
- Coupon discounts.
- Promotional discounts.
- Membership discounts.
- Discount rule engine.
- Real hardware integrations.
- Larger product and order volume testing.

## Implementation Philosophy

- Build vertically.
- Complete one business capability at a time.
- Each epic should end in a demonstrable feature.
- Avoid implementing infrastructure before a business scenario requires it.
- Prefer many small reviewable PRs over large feature drops.
