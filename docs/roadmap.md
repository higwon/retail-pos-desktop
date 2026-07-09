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

## Current Transition

Before the next product epic, the project is cleaning and consolidating documentation so future issues have a smaller source-of-truth set.

## Next Planned Area

POS core workflow completion is next before device simulation begins.

Candidate areas:

- Login MVP and current cashier/session context.
- POS main workflow state.
- Cart checkout button flow.
- Barcode entry fast path.
- Dashboard MVP binding.
- Payment simulation state hardening.
- Receipt print simulation boundary.
- Cashier happy path end-to-end validation.

## Later Planned Areas

Device simulation follows after the core cashier workflow is demonstrable:

- Barcode scanner simulator.
- Receipt printer simulator.
- Card reader simulator.
- Secondary monitor customer display.

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
