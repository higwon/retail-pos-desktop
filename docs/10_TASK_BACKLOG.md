# Task Backlog

## Status Note

- Task 1 solution structure and foundation: complete.
- Task 2 basic shell UI: complete.
- Task 3 domain models: complete.
- Task 4 local persistence and repeatable product seed data: complete through EPIC-04.
- Task 5 checkout MVP: complete through EPIC-05 (`POS-401` through `POS-409`).
- Task 6 API and synchronization: ready to begin with EPIC-06 (`POS-500` through `POS-508`).

This file is a coarse product backlog. For implementation order and scope, use the active
GitHub Issue together with `docs/13_EPICS_AND_TASKS.md`.

## Immediate Tasks

### Task 1: Create Solution Structure

Create the initial solution and projects.

Acceptance criteria:

- Solution builds.
- WPF desktop project runs.
- Project references follow the architecture document.
- Basic DI container is configured.
- MainWindow opens with an empty NavigationHost.
- No feature placeholder screens are created in Task 1.
- No domain, persistence, payment, synchronization, or UI feature logic is implemented in Task 1.

Codex implementation note:

- Read `README.md` and every document under `/docs` first.
- Follow `docs/00_AI_INSTRUCTIONS.md` strictly.
- Follow `docs/03_ARCHITECTURE.md` for project references.
- Do not implement Task 2 screens during Task 1.

### Task 2: Add Basic Shell UI

Create placeholder screens aligned with the Figma reference in `docs/11_UI_DESIGN.md`.

Figma reference:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

Screens:

- Login
- POS Main
- Payment Dialog placeholder
- Receipt Dialog placeholder
- Sync Status
- Admin Dashboard
- Checkout Recovery
- Customer Display

Acceptance criteria:

- User can navigate between placeholder screens.
- Customer Display can be opened as a separate WPF window placeholder.
- No business logic is required yet.
- Task 2 does not add domain, persistence, payment, or synchronization behavior.
- UI should use reusable WPF resources/styles where practical.
- UI should visually follow the Figma direction, but does not need pixel-perfect polish yet.

### Task 3: Add Domain Models

Implement initial domain model.

Models:

- Product
- Cart
- CartItem
- Order
- OrderLine
- Payment

Acceptance criteria:

- Cart can add/remove/update product quantity.
- Cart total is calculated.
- Domain tests pass.

### Task 4: Add Product Seed Data

Create local sample products.

Status: Complete through POS-303. Products are seeded idempotently during local database initialization and are readable through `IProductRepository`.

Acceptance criteria:

- Product seed data is repeatable and safe to initialize more than once.
- Seeded products can be read and searched through the local product repository.
- UI display and binding remain in POS-401.

### Task 5: Implement Checkout MVP

Implement local checkout without API sync.

Status: Complete. GitHub Issues POS-401 through POS-409 define the implemented checkout MVP scope.

Acceptance criteria:

- Cart can be checked out.
- Payment simulator returns approved/failed result.
- Approved order is saved locally.
- Cart is cleared after successful checkout.
- Customer Display updates when the cart changes and when payment state changes.
- PendingCheckout is saved before payment approval is requested.
- An approved checkout whose order was not created can be recovered after restart.

### Task 6: Add API and Synchronization MVP

Add the central API skeleton and local synchronization path for product download and
order upload.

Status: Todo. GitHub Issues POS-500 through POS-508 define the implementation order
and detailed scope.

Acceptance criteria:

- Data ownership and source-of-truth rules are documented before implementation.
- API project runs independently with a health endpoint.
- API exposes product sync and order upload contracts.
- Desktop can sync upstream product data into the local SQLite cache.
- Local order upload is idempotent and safe to retry.
- Desktop can process due order sync queue records with bounded retry behavior.
- Sync status UI shows pending, retrying, completed, and manual-review work.

## Later Tasks

- Add ASP.NET Core API.
- Add SQL Server persistence.
- Add order synchronization.
- Add sync retry policy.
- Add idempotency key handling.
- Add exponential backoff with at most 5 automatic retry attempts.
- Add real secondary-monitor placement for Customer Display.
- Add admin dashboard.
- Add performance test data.
- Add screenshots and demo guide.
- Add coupon, promotion, membership, and discount rule engine features after MVP.
- Add refund and order cancellation after MVP.

## Codex Task Prompt Template

Use this prompt when asking Codex to implement a task:

```text
Read README.md and every document under /docs first.
Follow docs/00_AI_INSTRUCTIONS.md strictly.
Use docs/11_UI_DESIGN.md and the Figma file as the UI reference when the task involves UI.

Implement only this task:
[task description]

Do not implement future roadmap items unless required for this task.
Keep the architecture consistent with docs/03_ARCHITECTURE.md.
After implementation, update docs if behavior or structure changed.
Build and test the solution before finishing.
```

## Recommended First Codex Prompt

```text
Read README.md and every document under /docs first.
Follow docs/00_AI_INSTRUCTIONS.md strictly.

Implement Task 1 from docs/10_TASK_BACKLOG.md only.

Create the initial .NET solution and project structure:
- RetailPOS.Desktop
- RetailPOS.Application
- RetailPOS.Domain
- RetailPOS.Infrastructure
- RetailPOS.Api
- RetailPOS.Domain.Tests
- RetailPOS.Application.Tests

Configure project references according to docs/03_ARCHITECTURE.md.
Configure basic dependency injection for the WPF desktop app.
Make MainWindow open with an empty NavigationHost.

Do not implement feature screens.
Do not implement domain models.
Do not implement persistence, payment, synchronization, or device logic.

Build the solution before finishing.
```
