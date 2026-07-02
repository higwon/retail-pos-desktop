# Roadmap

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
- Add Domain, Application, Infrastructure, Api, Shared projects.
- Add test projects.
- Configure project references.
- Add basic DI setup.
- Add basic WPF shell.
- Add placeholder navigation.

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
- Discount result
- Basic domain tests

Expected commit:

```text
feat: add core POS domain model
```

## Phase 3: Local SQLite Persistence

Goal: Store products and orders locally.

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

Tasks:

- Checkout service
- Payment simulator
- Receipt generator
- Receipt printer simulator
- Order completion UI

Expected commit:

```text
feat: implement checkout and receipt flow
```

## Phase 6: ASP.NET Core API

Goal: Add central server API.

Tasks:

- API project setup
- Auth endpoint
- Products endpoint
- Orders endpoint
- Health endpoint
- SQL Server persistence

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
