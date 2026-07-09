# Epics and Task Breakdown

This document defines the Jira-style task structure for the Retail POS Desktop project. It is the implementation scope source of truth for creating GitHub Issues and choosing the next PR.

All implementation work should be tracked by a `POS-XXX` task ID.

## Maintenance Rule

- Keep this file as an implementation planning source, not a changelog.
- Completed epics should be summarized, not expanded forever.
- New issues should be created from the current or next epic section.
- Detailed discussion belongs in GitHub Issues and PRs.
- If this file becomes too large, split only the completed historical detail, not the active planning source.

Use the same task ID in:

- GitHub Issue title
- Branch name when practical
- Commit message
- Pull request title
- Figma frame notes when the task is UI-related
- Documentation updates

Example commit message:

```text
feat(POS-001): create initial solution structure
```

## Workflow

```text
Backlog -> Todo -> In Progress -> Review -> Done
```

Recommended ownership:

- Product / architecture / UI review: ChatGPT
- Implementation / tests / refactoring: Codex
- Final decision: Repository owner

---

## EPIC-01 Application Skeleton

Goal: Create a clean, buildable application foundation before implementing features.

Status: Complete.

### POS-001 Create Solution Structure

Create the initial .NET solution and projects.

Scope:

- Solution file
- WPF desktop project
- Application project
- Domain project
- Infrastructure project
- API project
- Test projects

Acceptance criteria:

- Solution builds.
- WPF desktop project runs.
- No feature screens are implemented.
- No domain, persistence, sync, or device logic is implemented.

### POS-002 Configure Project References

Configure project references according to `docs/architecture.md`.

Acceptance criteria:

- Domain has no project dependencies.
- Application references Domain.
- Infrastructure references Application and Domain.
- Desktop references Application, Domain, and Infrastructure.
- API references Application, Domain, and Infrastructure.
- Tests reference only the projects required for each test scope.

### POS-003 Configure Dependency Injection

Add basic dependency injection for the WPF desktop app.

Acceptance criteria:

- Desktop app has a composition root.
- MainWindow is resolved through DI or is initialized with DI-created dependencies.
- Service registration is centralized.
- No static service locator pattern is introduced.

### POS-004 Add Navigation Shell

Add an empty navigation shell and host.

Acceptance criteria:

- MainWindow opens with a NavigationHost area.
- Navigation service abstraction exists if needed.
- No feature placeholder screens are added yet.

### POS-005 Add Base Theme Resources

Add initial ResourceDictionary files for colors, typography, buttons, and layout spacing.

Acceptance criteria:

- Resource dictionaries are structured for future screen styling.
- Theme values align with `docs/ui-guide.md`.
- No feature-specific styling is added yet.

### POS-006 Add Logging Foundation

Add dependency-injected logging infrastructure for Desktop and later Infrastructure services.

Acceptance criteria:

- App can log startup and unhandled errors.
- Logging abstraction uses `ILogger` or a compatible approach.
- No sensitive data is logged.
- Domain remains independent from logging.

### POS-007 Add Configuration Foundation

Add configuration structure for local development.

Acceptance criteria:

- App settings can be loaded.
- No secrets are committed.
- Typed options expose the configurable local DB path.
- The default database path is `%LOCALAPPDATA%\RetailPOS\retail-pos.db`.
- Domain and Application remain independent from configuration providers.

### POS-008 Add Global Exception Handling

Add global exception handling for WPF startup/runtime errors.

Acceptance criteria:

- Unhandled UI exceptions are logged.
- AppDomain exceptions are logged where practical.
- Task scheduler exceptions are logged where practical.
- App does not silently swallow exceptions.

---

## EPIC-02 UI Shell

Goal: Create WPF screen placeholders aligned with Figma.

Status: Complete.

### POS-101 Login Shell

Create the login screen placeholder.

### POS-102 POS Main Shell

Create the main POS screen placeholder.

### POS-103 Product Grid Shell

Create product search and grid placeholder.

### POS-104 Cart Panel Shell

Create cart panel placeholder.

### POS-105 Customer Display Shell

Create a customer-facing display window placeholder.

### POS-106 Payment Dialog Shell

Create payment dialog placeholder.

### POS-107 Receipt View Shell

Create receipt preview placeholder based on Figma `06 Receipt View`.

### POS-108 Checkout Recovery Shell

Create checkout recovery placeholder.

### POS-109 Dashboard Shell

Create dashboard placeholder based on Figma `07 Dashboard Screen`.

### POS-110 Status Screen Shell

Create status screen placeholder based on Figma `08 Status Screen`.

---

## EPIC-03 Domain Model

Goal: Implement testable business model without WPF dependencies.

Status: Complete.

### POS-201 Product Model

### POS-202 Cart Model

### POS-203 Order Model

### POS-204 Payment Model

### POS-205 Manual Discount Model

Implement only manual fixed-amount and percentage discounts for the MVP. Coupon, promotion, membership, and rule-engine discounts are Product Phase 2 scope.

### POS-206 Receipt Model

---

## EPIC-04 Local Persistence

Goal: Support offline-first local data storage.

Status: Complete.

### POS-301 SQLite Setup

### POS-302 Repository Interfaces

### POS-303 Local Product Seed Data

### POS-304 Local Order Persistence

### POS-305 PendingCheckout Persistence

### POS-306 Sync Queue Persistence

---

## EPIC-05 Checkout MVP

Goal: Complete a local-only checkout flow.

Status: Complete, with follow-up hardening tracked separately.

### POS-401 Product Search

### POS-402 Cart Operations

### POS-403 Manual Discount

### POS-404 Payment Simulator

### POS-405 PendingCheckout Flow

### POS-406 Order Completion

### POS-407 Customer Display Updates

### POS-408 Checkout Recovery

### POS-409 Receipt Generation and Preview

### POS-410 Harden Checkout Recovery Snapshot Validation

Follow-up hardening for incomplete or malformed persisted recovery snapshots. Unsafe
records should stay recoverable as manager-review candidates instead of crashing the
recovery screen.

---

## EPIC-06 API and Synchronization

Goal: Add server integration and reliable synchronization.

Status: Complete for the MVP API and synchronization path. Follow-up reliability,
observability, and automation work is tracked under later hardening tasks.

### POS-500 Document Data Ownership and Source of Truth

Clarify which records originate locally, which records come from upstream API/HQ data,
and how local SQLite acts as a cache or operational store after Checkout MVP.

### POS-501 ASP.NET Core API Skeleton

Create the first runnable API surface for EPIC-06.

Scope:

- Configure API startup and route grouping under `/api`.
- Add `GET /api/health`.
- Add global exception handling that returns ProblemDetails.
- Add status-code ProblemDetails for empty error responses.
- Add request logging with method, path, status code, and elapsed time.
- Add focused tests for API middleware behavior.

### POS-502 Product Sync API Contract

Define the first product synchronization API contract.

Scope:

- Add `GET /api/products` under the API route group.
- Support `updatedAfter`, `page`, and `pageSize` query parameters.
- Return product DTOs with `version`, `updatedUtc`, and `isActive`.
- Keep the server implementation empty or placeholder-only until real API persistence is added.
- Document that upstream HQ/API is the production product source of truth and local SQLite is a cache.

### POS-503 Desktop Product Sync Client and Upsert

Add the desktop-side product sync boundary and local cache upsert behavior.

Scope:

- Add an application-level product sync service and client/store abstractions.
- Add an infrastructure HTTP product sync client for `GET /api/products`.
- Add SQLite product cache upsert support.
- Persist `stockQuantity`, `version`, and `updatedUtc` in the local product cache.
- Treat `isActive = false` as a soft-delete/inactive product update.
- Ignore older product versions so stale sync responses do not overwrite newer local cache data.

### POS-504 Order Upload API Contract

Define the completed local order upload API contract.

Scope:

- Add `POST /api/orders` under the API route group.
- Require `schemaVersion`, `storeId`, `terminalId`, `localOrderId`, `idempotencyKey`, and `businessDate`.
- Validate UTC timestamps, whole-KRW money values, line totals, and approved payment totals.
- Return an order upload response shape while keeping persistence/idempotency implementation placeholder-only.
- Document that real idempotency behavior is implemented in POS-505.

### POS-505 API Idempotency Handling for Order Upload

Handle duplicate order upload requests safely at the API boundary.

Scope:

- Add an order upload idempotency store abstraction.
- Treat `storeId + terminalId + localOrderId` as the order upload identity.
- Return the existing server order result when the same upload is retried.
- Return `409 Conflict` when an idempotency key is reused for a different order identity.
- Return `409 Conflict` when an existing order identity is retried with a different idempotency key.
- Return duplicate-safe `Synced` responses from the API skeleton.
- Keep storage in memory until durable server persistence is introduced.

### POS-506 Desktop Order Sync Worker and Retry Policy

Upload due pending local order sync queue records to the API.

Scope:

- Add an application-level order sync worker/use case.
- Read due pending `SyncQueue` records for order uploads.
- Deserialize the full order upload payload produced by POS-508.
- Upload through an order upload API client abstraction.
- Mark queue items completed after successful or idempotent duplicate-safe upload.
- Catch upload failures so sync does not crash the POS app.
- Update retry count, next attempt UTC, and last error summary on failure.
- Use bounded exponential backoff for automatic attempts: 1m, 2m, 4m, 8m, 16m.
- Stop automatic upload attempts after 5 failures by marking the queue item `Exhausted` for manual/status review.
- Treat order upload idempotency conflicts as non-retryable and mark the queue item `Exhausted`.

### POS-507 Sync Status UI Integration

Bind the Desktop status screen to local sync queue state.

Scope:

- Add an application-level sync status snapshot service.
- Show pending, retrying, completed, and manual-review queue counts.
- Show recent sync queue records and selected item details.
- Add manual refresh and run-sync commands.
- Invoke the POS-506 order sync worker from the status screen.
- Keep sensitive or overly technical failure details out of user-facing status text.

### POS-508 Align Local Order Sync Queue Payload with Upload Contract

Expand the local order sync queue payload so it contains the full completed-order data
required by the API upload contract before the Desktop sync worker consumes it.

---

## EPIC-07 POS Workflow Completion

Goal: Close the cashier-facing POS workflow before adding richer device simulators.

Status: Planned next.

Reason:

- `PosMainViewModel` is still a shell and does not own the main register workflow.
- The cart checkout button is disabled, so the cashier payment path currently relies on a top-bar shortcut.
- Barcode entry is not yet a fast path from scan/input to cart.
- Dashboard values are hardcoded and not bound to operational data.
- Payment and receipt simulation boundaries need to be shaped before device simulation grows around them.

### POS-601 Login MVP and Session Context

Implement the MVP login workflow and current session context used by the POS shell.

Scope:

- Replace the direct sign-in bypass with a ViewModel command.
- Validate employee code and password through an application-level login service.
- Establish a current cashier/session context after successful login.
- Show user-safe validation and authentication failure messages.
- Keep the implementation compatible with later cached offline login.

Acceptance criteria:

- Sign-in does not navigate to POS on invalid input.
- Successful sign-in exposes cashier/session information for the POS shell.
- Login failure messages are safe and non-technical.
- Unit tests cover successful login, invalid input, and failed login.

### POS-602 POS Main Workflow State

Turn the POS main shell into a workflow-aware register surface.

Scope:

- Add state to `PosMainViewModel` for cashier, store/terminal, connectivity, cart summary, and sync indicators.
- Replace hardcoded cashier/online text in the POS header with bound values.
- Keep navigation and dialog opening behavior thin in the View.
- Prepare the shell to react to future login, sync, and connectivity state changes.

Acceptance criteria:

- POS header shows bound cashier/session and connectivity values.
- ViewModel exposes workflow state without referencing Views.
- Existing product/cart/payment flows continue to work.
- Tests cover the ViewModel state mapping.

### POS-603 Cart Checkout Button Flow

Make the cart's checkout button the primary payment entry point.

Scope:

- Enable the cart checkout button when the cart has a positive total.
- Route checkout through a command or event that opens the payment dialog.
- Remove or demote the top-bar Payment shortcut so cashier flow starts from the cart.
- Keep receipt dialog behavior after approved payment.

Acceptance criteria:

- Empty cart cannot start checkout.
- Cart checkout opens the payment dialog for a payable cart.
- Approved payment still creates the order, receipt preview, and sync queue item.
- Tests cover checkout command availability and event/command behavior.

### POS-604 Barcode Entry Fast Path

Add a cashier-friendly barcode entry flow.

Scope:

- Add barcode input to the product area or POS shell.
- On Enter/scanner-like input, look up an active product by barcode.
- Add a found product directly to the cart.
- Show a user-safe not-found message without clearing the current cart.
- Preserve text search and manual Add behavior.

Acceptance criteria:

- Known barcode adds the matching product to the cart.
- Unknown barcode shows a safe message and does not change the cart.
- Barcode input is trimmed and treated as a fast path.
- Tests cover found and not-found barcode flows.

### POS-605 Dashboard MVP Binding

Replace dashboard mock values with MVP operational data.

Scope:

- Bind dashboard cards to real local data where available.
- Show today's order count and sales total.
- Show pending/retrying/exhausted sync counts.
- Show checkout recovery count.
- Show recent local orders.
- Show API connectivity status from the existing connectivity monitor state.
- Remove broken or placeholder currency text.

Acceptance criteria:

- Dashboard no longer displays hardcoded sales/order/sync values.
- Dashboard handles empty data with useful zero states.
- User-facing messages are safe and non-technical.
- Tests cover dashboard snapshot calculation.

### POS-606 Payment Simulation States Hardening

Prepare payment simulation for later card-reader device simulation.

Scope:

- Extend payment simulation states beyond approve/fail where useful for MVP hardening.
- Add timeout, cancelled, and communication-error style outcomes as application-level results.
- Keep order completion limited to approved payment results.
- Keep user-facing messages safe and distinct for each outcome.

Acceptance criteria:

- Only approved payment completes an order.
- Non-approved outcomes leave the cart recoverable or unchanged according to the current checkout rules.
- Payment dialog can surface the new outcomes safely.
- Tests cover each payment simulation outcome.

### POS-607 Receipt Print Simulation Boundary

Separate receipt print simulation from receipt generation.

Scope:

- Introduce an application-level printer abstraction for receipt printing.
- Move the current print stub behind that abstraction.
- Keep receipt generation independent from printer simulation.
- Prepare the boundary for a richer receipt printer simulator in the next epic.

Acceptance criteria:

- Receipt generation still works without a printer.
- Print command uses the printer abstraction.
- Print success and failure messages are user-safe.
- Tests cover print success and simulated failure.

### POS-608 Cashier Happy Path End-to-End Validation

Validate the core cashier path before device simulation begins.

Scope:

- Cover login, barcode/search, cart, discount, checkout, payment, receipt, and sync queue creation.
- Prefer an automated integration-style test where practical.
- Add a short manual demo checklist if a UI-only step cannot be automated cleanly.
- Keep the test deterministic and independent of developer machine state.

Acceptance criteria:

- The happy path can be demonstrated from login to queued order sync.
- The validation covers at least one discount and approved payment.
- The validation confirms receipt preview availability.
- The validation confirms a sync queue item is created.

---

## EPIC-08 Device Simulation

Goal: Demonstrate Windows POS peripheral integration concepts.

Status: Planned after EPIC-07 closes the core cashier workflow.

### POS-701 Barcode Scanner Simulator

### POS-702 Receipt Printer Simulator

### POS-703 Card Reader Simulator

### POS-704 Secondary Monitor Customer Display

---

## EPIC-09 Production Readiness

Goal: Improve reliability, performance, test coverage, and portfolio presentation.

Status: Planned after the core workflow and device simulation epics. Some sync
hardening work was completed early under the historical POS-711 through POS-715 IDs.

### POS-801 Unit Tests

### POS-802 Integration Tests

### POS-803 Performance Test Data

### POS-804 Error Handling Polish

### POS-805 UI Polish

### POS-806 Demo Guide

### POS-807 Portfolio Summary

### POS-808 Configuration and Environment Hardening

Review appsettings, local development defaults, API/Desktop endpoint configuration,
and machine-specific paths.

### POS-809 Logging and Audit Hardening

Strengthen operational logging, audit-relevant events, and safe diagnostic context
without logging sensitive data.

### POS-810 Offline and Recovery Scenario Tests

Exercise offline/online transitions, interrupted checkout recovery, sync retries, and
large product/order scenarios.

### POS-711 Add Sync Integration Tests from SQLite Queue to API Upload

Add end-to-end style tests that cover the real local sync path across SQLite
repositories, the order sync worker, and the API order upload boundary.

Scope:

- Use a temporary SQLite database for local queue/order state.
- Exercise due `SyncQueue` order records through `OrderSyncService`.
- Send uploads to an in-process API host or equivalent test HTTP boundary.
- Verify successful uploads mark queue items completed.
- Verify idempotency conflicts and validation failures become manual-review work
  instead of crashing the POS app.
- Keep the tests deterministic and isolated from developer machine state.

### POS-712 Add Background Order Sync Scheduler

Run order synchronization automatically while the Desktop app is open.

Scope:

- Add a Desktop background scheduler using the existing `OrderSyncService`.
- Use bounded periodic execution with cancellation on app shutdown.
- Avoid overlapping sync runs.
- Respect existing retry due times and max automatic retry behavior.
- Log scheduler start, completion, skip, and failure events.
- Keep manual `Run sync` available from the Status screen.

### POS-713 Add API Connectivity Monitor and Reconnect-Triggered Sync

Detect whether the configured API is reachable and trigger sync after recovery.

Scope:

- Add an application-level connectivity abstraction.
- Prefer API health checks over OS network status as the source of truth.
- Track online, offline, and degraded states.
- Trigger a bounded sync attempt when the API becomes reachable again.
- Surface connectivity state for future header/status UI binding.
- Avoid tight retry loops while the API remains unavailable.

### POS-714 Add Messenger-Based Sync Status Refresh

Reduce manual refresh dependence by notifying view models when sync state changes.

Scope:

- Use CommunityToolkit messenger patterns already used by the Desktop stack.
- Publish messages after sync run completion, queue exhaustion, and connectivity changes.
- Refresh `StatusViewModel` automatically when relevant messages arrive.
- Define unregister/disposal rules for message-recipient view models.
- Keep user-safe failure text in the UI.

### POS-715 Add Serilog Structured Logging and Sync Audit Events

Introduce structured operational logs for Desktop and API diagnostics.

Scope:

- Add Serilog through the existing `ILogger` abstraction.
- Write Desktop logs under the configured local app data directory.
- Keep API logs console-friendly for local development and CI.
- Log sync run start/completion/failure, exhausted queue items, idempotency conflicts,
  and connectivity changes with structured properties.
- Do not log sensitive payment, auth, token, or secret data.
