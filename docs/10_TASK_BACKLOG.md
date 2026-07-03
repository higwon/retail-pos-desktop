# Task Backlog

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

Acceptance criteria:

- App can display seeded products.
- Product search works against local data.

### Task 5: Implement Checkout MVP

Implement local checkout without API sync.

Acceptance criteria:

- Cart can be checked out.
- Payment simulator returns approved/failed result.
- Approved order is saved locally.
- Cart is cleared after successful checkout.
- Customer Display updates when the cart changes and when payment state changes.
- PendingCheckout is saved before payment approval is requested.
- An approved checkout whose order was not created can be recovered after restart.

## Later Tasks

- Add receipt generator.
- Add receipt printer simulator.
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
- RetailPOS.Shared
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
