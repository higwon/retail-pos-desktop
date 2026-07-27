# Phase 2 Product Plan

## Status

Future product-expansion backlog. Phase 2 is not part of the completed portfolio release,
and no implementation schedule is currently committed.

This document records product direction at epic level. Detailed `POS-XXX` tasks,
milestones, and implementation commitments should be created only when an epic is selected
for active development.

## Objective

Extend the completed offline-first checkout foundation into post-sale retail operations,
durable server persistence, selected real-device integration, and deployable Windows
delivery without weakening the existing recovery and duplicate-prevention guarantees.

The target business path is:

```text
Sale
-> payment and local completion
-> durable server synchronization
-> receipt lookup
-> cancellation or refund
-> inventory and settlement evidence
-> shift and business-day review
```

## Proposed Delivery Order

1. Durable server persistence.
2. Cancellation and refund.
3. Discount and promotion.
4. Shift and business-day closing.
5. Real device adapter pilot.
6. Windows deployment and update.

This order is a planning hypothesis, not a committed schedule. Packaging and migration
safety may be brought forward when active implementation begins so database changes are
validated against an installed application early.

## Proposed Epics

### EPIC-11 Durable Server Persistence

Persist uploaded orders and idempotency records in a server database so duplicate
protection survives API restarts. Preserve the Desktop's existing offline-first checkout
path and use database constraints, transactions, and concurrency tests to enforce a single
server result for duplicate uploads.

Candidate scope:

- API database foundation and migrations.
- Durable order, order-line, payment, and idempotency records.
- Stable order identity using `storeId + terminalId + localOrderId`.
- Bounded server order queries needed by later operations.
- Restart, response-loss, conflict, and concurrent-duplicate integration tests.

### EPIC-12 Cancellation and Refund

Support full cancellation and quantity-based partial refunds without modifying or deleting
the original completed order. Represent every correction as a new auditable record and keep
payment outcomes recoverable and duplicate-safe.

Candidate scope:

- Cancellation and refund policy, eligibility, reason, and manager-approval evidence.
- Receipt lookup with remaining refundable quantities and prior adjustment history.
- Full cancellation and repeated partial refunds with deterministic discount allocation.
- Separate cash-return and card-refund behavior.
- Recoverable `Unknown` card-refund outcomes and manager review.
- Durable local persistence, ordered synchronization, server validation, and refund receipt.

### EPIC-13 Discount and Promotion

Expand the current manual fixed and percentage discounts into explicit coupon, promotion,
and membership discount applications while preserving a reproducible calculation breakdown.
Do not introduce a generic rule language until repeated requirements justify it.

Candidate scope:

- Immutable discount evidence and line allocation.
- Coupon validity and duplicate-use protection.
- A small set of explicit product and category promotions.
- Discount priority and stacking policy.
- Minimal membership lookup and privacy-safe presentation if membership remains in scope.
- Stable receipt, order, and refund calculations based on the original evidence.

### EPIC-14 Shift and Business-Day Closing

Track register shifts, cash movements, tender totals, discrepancies, and business-day closing
results as auditable operational records.

Candidate scope:

- Register shift open and close lifecycle.
- Opening cash, paid-in, paid-out, cash pickup, and adjustment evidence.
- Expected and counted cash with discrepancy approval.
- Sales, cancellation, refund, and discount summaries.
- Warnings for unresolved payments and unsynchronized work.
- Immutable closing results and explicit correction records.

### EPIC-15 Real Device Adapter Pilot

Implement one real scanner path while preserving the current simulator and application-owned
device boundaries. The Register workflow must remain unaware of the selected adapter.

Candidate scope:

- Keyboard-wedge scanner pilot.
- Optional serial scanner only if real requirements justify it.
- Configuration-based adapter selection.
- Connection, reconnect, cancellation, diagnostics, and safe logging.
- Documented printer and payment-terminal vendor SDK boundaries without speculative adapters.

### EPIC-16 Windows Deployment and Update

Produce an installable Windows delivery with safe local-data migration and a repeatable
release workflow.

Candidate scope:

- Self-contained `win-x64` Release publish.
- Installer technology selection and install/uninstall behavior.
- Preservation of `%LOCALAPPDATA%\RetailPOS` application data.
- Database backup and fail-closed migration startup policy.
- Versioned CI artifacts, checksums, and release notes.
- Optional update-check foundation; forced updates remain out of scope.

## Scope Principles

- Build one vertical business capability at a time.
- Preserve the existing offline-first checkout and fail-closed recovery behavior.
- Do not modify completed orders; represent corrections as new records.
- Keep local and server identities stable across retries.
- Treat authorization, audit evidence, and user-safe failure states as part of business scope.
- Keep simulator controls outside cashier business interfaces.
- Avoid generic promotion, device, or workflow frameworks until repeated requirements justify
  them.
- Keep each active epic independently reviewable and demonstrable.

## Deferred Decisions

The following choices must be revisited with current requirements before implementation:

- Server database provider and deployment topology.
- Authentication, authorization, and manager-override provider.
- Cancellation-versus-refund business policy and business-day restrictions.
- Offline cash-return and card-refund execution policy.
- Payment vendor SDK and refund capabilities.
- Installer technology and signing strategy.
- Update distribution channel.
- Inventory authority and settlement integration boundaries.

## Out of Scope for This Plan

- A committed schedule or milestone.
- Detailed `POS-XXX` implementation tasks.
- Production vendor selection or commercial integration commitments.
- A generic promotion rule language.
- Simultaneous implementation of every device adapter.
- Claims that any Phase 2 capability is already implemented.

## Starting an Epic

Before moving an epic from future backlog into active development:

1. Revalidate its business policy and dependencies against the current codebase.
2. Record the selected deferred decisions in the appropriate source-of-truth documents.
3. Split only the selected epic into focused `POS-XXX` issues with acceptance criteria.
4. Create a milestone only when delivery scope and timing are intentionally committed.
5. Keep the remaining Phase 2 epics in future backlog.
