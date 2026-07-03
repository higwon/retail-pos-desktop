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

### Task 2: Add Basic Shell UI

Create placeholder screens.

Screens:

- Login
- POS Main
- Payment Dialog placeholder
- Receipt Dialog placeholder
- Sync Status
- Admin Dashboard
- Checkout Recovery

Acceptance criteria:

- User can navigate between placeholder screens.
- No business logic is required yet.
- Task 2 does not add domain, persistence, payment, or synchronization behavior.

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

Implement only this task:
[task description]

Do not implement future roadmap items unless required for this task.
Keep the architecture consistent with docs/03_ARCHITECTURE.md.
After implementation, update docs if behavior or structure changed.
Build and test the solution before finishing.
```
