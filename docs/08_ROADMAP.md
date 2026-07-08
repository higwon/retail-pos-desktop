# Roadmap

## Current Status

- Phase 0 documentation: complete
- Phase 1 solution setup: complete
- Phase 2 domain model MVP: complete
- UI shell placeholders: complete ahead of the original phase ordering
- Phase 3 local SQLite persistence: complete
- Phases 4 and 5 / EPIC-05 Checkout MVP: complete
- Phase 6 / EPIC-06 API and synchronization: ready to start

Phase numbers describe capability groups, not a strict implementation sequence. GitHub
Issues and `docs/13_EPICS_AND_TASKS.md` define the active implementation order.

## Phase 0: Documentation

Goal: Prepare the project for Codex-driven implementation.

Tasks:

- Create README.
- Create docs.
- Define architecture.
- Define MVP scope.

## Phase 1: Solution Setup

Goal: Create the initial buildable solution.

Tasks:

- Create .NET solution.
- Add WPF desktop project.
- Add Domain, Application, Infrastructure, and Api projects.
- Add test projects.
- Configure project references.
- Add basic DI setup.
- Add basic WPF shell.
- Add an empty NavigationHost.

Expected commit:

```text
feat: setup initial solution structure
```

## Phase 2: Domain Model MVP

Goal: Implement core business objects.

Tasks:

- Product
- Cart
- CartItem
- Order
- OrderLine
- Payment
- Manual discount result
- Basic domain tests

Feature screen placeholders are created as a separate task after the initial solution setup. They are not part of the first implementation task.

Expected commit:

```text
feat: add core POS domain model
```

## Phase 3: Local SQLite Persistence

Goal: Store products and orders locally.

Status: Complete. SQLite setup, repository contracts and implementations, repeatable
product seed data, order persistence, checkout recovery records, sync queue records,
and the shared local transaction boundary are implemented.

Tasks:

- Add SQLite setup.
- Add local entities/mapping.
- Add repositories.
- Add seed data.
- Add local order save.

Expected commit:

```text
feat: add local SQLite persistence
```

## Phase 4: POS Main UI

Goal: Build usable cashier screen.

Status: Shell UI is complete. EPIC-05 adds data binding and behavior through POS-401 onward.

Tasks:

- Product search UI
- Product grid/cards
- Cart panel
- Total panel
- Payment buttons
- Keyboard shortcuts

Expected commit:

```text
feat: implement POS main screen
```

## Phase 5: Checkout and Receipt

Goal: Complete local checkout flow.

Status: Complete through EPIC-05 Checkout MVP.

Tasks:

- Checkout service
- Payment simulator
- Receipt generator
- Receipt printer simulator
- Order completion UI
- PendingCheckout persistence before payment approval
- Approved checkout recovery after restart

Expected commit:

```text
feat: implement checkout and receipt flow
```

## Phase 6: ASP.NET Core API

Goal: Add central server API.

Status: Ready to start under EPIC-06 API and Synchronization. Begin with data
ownership documentation, then API skeleton, product sync, order upload, idempotency,
local sync payload alignment, sync worker/retry, and sync status UI.

Tasks:

- Document data ownership and source of truth
- API project setup
- Health endpoint
- Product sync endpoint
- Order upload endpoint
- Idempotency handling
- Local order sync queue payload alignment
- Desktop product sync client/upsert
- Desktop order sync worker and retry policy
- Sync status UI integration
- Authentication and SQL Server persistence later in EPIC-06 or a follow-up phase

Expected commit:

```text
feat: add POS server API
```

## Phase 7: Offline Sync

Goal: Synchronize local orders with server.

Tasks:

- SyncQueue table
- Sync service
- Retry logic
- Idempotency key support
- Sync status UI

Expected commit:

```text
feat: add offline order synchronization
```

## Phase 8: Admin Dashboard

Goal: Add manager-level visibility.

Tasks:

- Sales summary
- Order history
- Failed sync list
- Stock overview

Expected commit:

```text
feat: add admin dashboard
```

## Phase 9: Performance and Reliability

Goal: Improve engineering depth.

Tasks:

- Large product dataset testing
- Large order history testing
- Async UI improvements
- Logging improvements
- Error recovery
- Retry policy refinement
- Exponential backoff with a maximum of 5 automatic attempts

Expected commit:

```text
refactor: improve reliability and sync performance
```

## Phase 10: Portfolio Polish

Goal: Make the repository presentable.

Tasks:

- Add screenshots.
- Add architecture diagram.
- Add demo scenario.
- Add sample seed data guide.
- Add known limitations.
- Add future improvements.

## Product Phase 2: Post-MVP Features

This product phase is separate from the numbered implementation phase named `Phase 2: Domain Model MVP`.

- Coupon discounts
- Promotional discounts
- Membership discounts
- Discount rule engine
- Refund workflow
- Order cancellation workflow
