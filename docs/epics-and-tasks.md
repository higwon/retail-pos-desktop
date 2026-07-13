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

Status: Complete.

Completed scope:

- Demo login establishes the current cashier and terminal session.
- The POS header and dashboard bind to operational state.
- Cart checkout is the primary payment entry point.
- Barcode entry supports a fast path while preserving product search.
- Payment outcomes are hardened and receipt printing has an application-level boundary.
- The cashier happy path is validated through SQLite order and sync-queue persistence.

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

## Device Simulation Readiness

Goal: Lock device boundaries and harden delayed payment execution before EPIC-08
simulators add latency, cancellation, connection state, and indeterminate outcomes.

Status: Complete.

### POS-609 Device Simulation Architecture

Align roadmap and epic status, document business-port and simulator-control ownership,
define lifecycle/cancellation/UI-thread rules, and make the Barcode Scanner and
Customer Display boundaries explicit.

Acceptance criteria:

- EPIC-07 is complete and EPIC-08 is the next planned area.
- POS-701 through POS-704 have concrete scope and acceptance criteria.
- Current implementation is clearly separated from accepted target policy.
- Simulator controls do not leak into Application business contracts.
- Device lifetime, cancellation, UI-thread, and payment Unknown policies are explicit.

### POS-610 Payment Terminal Boundary Hardening

Replace simulation-shaped Application contracts with a production-shaped card terminal
port, separate cash processing, prevent overlapping payment attempts, propagate
cancellation, and preserve indeterminate outcomes for review.

Acceptance criteria:

- Application payment requests contain no simulator scenario selection.
- Card and cash paths are separate and mutually exclusive.
- UI and Application gates prevent overlapping pending checkouts.
- Delayed operations support cancellation and dialog-close behavior.
- Timeout, communication loss, and unconfirmed post-dispatch cancellation become
  Unknown rather than Failed.
- Unknown payment attempts do not create orders or clear carts and remain discoverable
  for review.

---

## EPIC-08 Device Simulation

Goal: Demonstrate Windows POS peripheral integration concepts.

Status: Complete for the original device-simulation scope. Interactive request/response
workflows and cashier UX follow-ups are tracked below before EPIC-09.

Delivery guidance:

- Recommended implementation order is POS-702, POS-701, POS-703, then POS-704.
- Business ports expose normal device operations only.
- Simulator scenario controls stay outside cashier business commands.
- Real hardware SDK integration remains Phase 2.

### POS-701 Barcode Scanner Simulator

Simulate an event-producing barcode scanner while preserving manual and
keyboard-wedge entry.

Scope:

- Add an application-level scanner event boundary.
- Keep scanner scenario and connection controls outside the business port.
- Deliver scanned barcodes through a Desktop coordinator to the existing local lookup
  and cart-add flow.
- Marshal scanner callbacks to the WPF dispatcher before observable UI updates.
- Keep TextBox/Enter barcode entry as manual input and keyboard-wedge fallback.
- Define terminal-scope subscription, unsubscription, and disposal behavior.

Acceptance criteria:

- A connected simulator can emit a barcode without mutating a ViewModel TextBox.
- A known scanned barcode adds the active product through the existing checkout flow.
- An unknown barcode shows a user-safe message and does not change the cart.
- Background-thread scanner callbacks do not update WPF-bound collections directly.
- Disconnect, reconnect, repeated scan, cancellation, and disposal behavior are tested.
- Manual barcode entry continues to work without the scanner simulator.

### POS-702 Receipt Printer Simulator

Expand the existing receipt-printer adapter into a controllable simulator without
changing receipt generation.

Scope:

- Keep `IReceiptPrinter` as the business-facing print port.
- Add explicit print outcomes for printed, paper-out, cover-open, disconnected,
  timeout, cancelled, busy, and unexpected failure.
- Add simulator controls for next outcome, response delay, and connection state
  outside the Application business contract.
- Add a Desktop-only, modeless Device Simulator window organized by device tabs.
- Implement the Receipt Printer tab first; later device tasks extend the same window.
- Model device-specific ready/printing/busy/fault behavior.
- Preserve user-safe UI messages and allow retry after recoverable failures.

Acceptance criteria:

- Receipt generation and preview work when no printer is available.
- Successful print records a UTC completion timestamp.
- Paper-out, cover-open, disconnected, timeout, cancellation, and busy outcomes are
  distinguishable without parsing message strings.
- Only one print operation runs at a time.
- Delayed print supports cancellation and does not leave stale success/error UI state.
- Retry after a recoverable printer outcome is covered by tests.

### POS-703 Card Reader Simulator

Implement a delayed, stateful card-terminal simulator on top of the POS-610 payment
business boundary.

Scope:

- Depend on the production-shaped `IPaymentTerminal` contract from POS-610.
- Add simulator controls for next outcome, response delay, and connection state.
- Model card-terminal states such as idle, waiting for card, processing, approved,
  declined, cancelled, unknown, and faulted.
- Keep scenario controls out of cashier payment commands.
- Preserve pending-checkout identity across authorization and review.
- Treat timeout, communication loss, and unconfirmed post-dispatch cancellation as
  Unknown.

Acceptance criteria:

- The cashier requests normal card authorization without selecting a scenario.
- Approve, decline, confirmed cancellation, timeout, communication loss, unknown,
  disconnected, busy, and delayed responses are deterministic in tests.
- Only approved authorization completes an order.
- Unknown authorization does not create an order, clear the cart, or allow silent
  immediate retry.
- Payment commands remain mutually exclusive throughout delayed terminal work.
- Closing the payment dialog follows the POS-610 cancellation and Unknown policy.

### POS-704 Secondary Monitor Customer Display

Host the existing customer-display data on one Desktop-owned Windows display window.

Scope:

- Keep checkout/cart/payment display data in the existing ViewModel and display state.
- Add a Desktop display host that discovers available monitor targets.
- Own at most one customer-display window per terminal UI scope.
- Place the display fullscreen on the selected monitor.
- Handle monitor disconnect, target changes, DPI changes, and no-secondary-monitor
  fallback safely.
- Close and release the display window with its terminal UI scope.

Acceptance criteria:

- Available display targets can be enumerated without opening duplicate windows.
- Opening an already-open customer display reuses or activates the owned window.
- The selected monitor receives a correctly placed fullscreen display.
- Disconnecting the selected monitor produces a safe fallback and keeps the POS usable.
- Cart, discount, payment-waiting, failure, and completion states continue to update.
- Closing the terminal UI scope closes and disposes the customer display.
- Monitor selection and placement logic have focused tests where practical.

---

## EPIC-08 Follow-up: Interactive Devices and POS UX

Goal: Turn the device simulator into an operator-driven integration demo and remove
the most visible cashier workflow and layout friction before production-readiness work.

Status: Complete.

Delivery guidance:

- Recommended order is POS-705, POS-706/POS-707, POS-708, POS-709, POS-710,
  then POS-716.
- Simulator requests and responses must remain separate from production business ports.
- Do not block simulator interaction behind modal cashier windows.
- Preserve fail-closed payment and pending-checkout policies.

Completed scope:

- Shared bounded request lifecycle for interactive printer and card workflows.
- Operator responses with typed outcomes and safe request/history payloads.
- Product picker and manual barcode modes for scanner simulation.
- Cashier-facing device connectivity and display topology updates.
- Filterable health-and-beauty catalog with direct tile-to-cart interaction.
- Safe sign-out/session teardown and high-visibility layout polish.

### POS-705 Interactive Device Request/Response Foundation

Replace preselected one-shot device outcomes with pending request sessions that expose
POS request data to the simulator and wait for an operator response.

Acceptance criteria:

- The shared request lifecycle is explicitly limited to Pending, Completed, Cancelled,
  TimedOut, Disconnected, and Disposed.
- Only Pending may transition to one terminal state; every later transition or duplicate
  response is rejected deterministically.
- Printer and card requests have an immutable payload, independent request identity,
  received/completed timestamps, payload summary, and preserved business identity such as
  receipt/order or payment-attempt ID without conflating the two identities.
- Requests preserve arrival order and use a documented per-device single-active-request or
  bounded-pending policy.
- Completion continuations run asynchronously; no external event/callback is invoked while
  an internal lock is held.
- The simulator receives requests without cashier ViewModels depending on simulator controls.
- Pending requests support cancellation, timeout, disconnect, disposal, and late-response rejection.
- A bounded in-memory recent history retains the latest 20 to 50 completed requests with
  request ID, device, received/completed time, result, and safe payload summary.
- The simulator distinguishes Pending Requests from Recent Completed Requests.
- Simulator controls remain usable while receipt/payment workflow windows are open; dialog
  ownership is changed from modal where required without allowing duplicate workflow windows.
- The Device Simulator presents a polished shared request queue/detail pattern without a
  premature generic hardware framework.

### POS-706 Interactive Receipt Printer Workflow and Receipt Status UX

Show print request details and complete printing only after the simulator operator sends a
typed result.

Acceptance criteria:

- A print request displays receipt/order identity, store/register/cashier data, line items,
  totals, payments, and requested time in the simulator.
- Structured receipt data is formatted into a plain-text printable preview visible before response;
  generating real ESC/POS bytes remains out of scope.
- The operator can respond Printed, Paper out, Cover open, Disconnected, Timeout, Cancelled,
  or Failed; the POS receives exactly that typed result.
- Busy is an automatic rejection for a new request while the device already has an active request,
  not an operator-selected response for the current pending request.
- Receipt preview remains usable while a print request is pending and the simulator can be
  operated at the same time.
- Retry creates a new request ID while preserving the same receipt/order identity, and the POS
  never reports print success before a Printed response.
- A retry after failure replaces stale failure/success feedback clearly.
- Receipt status/success/error text uses one consistent location near the header instead of
  splitting status and error feedback between the top and bottom.
- Retry and late/duplicate response behavior have focused tests.

### POS-707 Interactive Card Terminal Workflow and Payment Result Layout

Show authorization request data in the simulator and let the operator send the terminal result.

Acceptance criteria:

- The simulator shows payment-attempt identity, amount, request time, terminal state, and
  pending-checkout-safe context without exposing sensitive card data.
- The operator can send Approve, Decline, confirmed Cancel, Timeout, Communication loss, or
  Unknown; only Approve completes an order.
- Approve uses deterministic default approval code and transaction reference values derived
  safely from the request/attempt; the operator may edit those two values only.
- Approved amount always equals the requested whole-KRW amount and approved timestamp is UTC;
  partial approval or operator-edited amount is out of scope.
- A payment attempt can be approved only once. Unknown or any other terminal result cannot be
  changed by a late Approve response.
- Approval code and transaction reference are generated/displayed without overlapping or clipping.
- Closing payment UI, disconnect, timeout, and responses after cancellation preserve the
  POS-610 Unknown and review policy.
- Overlap, duplicate response, and attempt-identity preservation are tested.
- If POS UI closes after response, persistence/recovery still records the terminal result.
- Request/history payloads never contain card number, track data, card token, or equivalent
  sensitive payment data.

### POS-708 Barcode Scanner Product Picker

Let the simulator operator choose an active product instead of requiring knowledge of raw
barcode values.

Acceptance criteria:

- The Barcode Scanner tab loads active products with name, SKU, category, price, and barcode.
- Products can be searched by name, SKU, or barcode and filtered by the same category source
  used by POS-710; large result sets use UI virtualization.
- Product Picker and Manual Barcode are visibly distinct modes.
- The selected product barcode is previewed; Emit Scan keeps the selection so rapid repeated
  scans of the same item remain convenient.
- Manual barcode entry remains available for unknown/error testing.
- Products without a usable barcode cannot be emitted and show a safe explanation.
- Existing background-callback, disconnect/reconnect, and repeated-scan behavior remains covered.

### POS-709 POS Device Connectivity Status

Expose cashier-facing device readiness separately from developer simulator controls.

Acceptance criteria:

- The common UI model exposes Availability (Available, Unavailable, Disabled), Readiness
  (Ready, Busy, Attention, Unknown), and a device-specific user-safe Detail string.
- Initial Unknown, simulator Disabled, and physical/device Unavailable remain distinguishable.
- Last-change time is stored in UTC and displayed in local time.
- State refreshes automatically, serializes event bursts safely, and does not expose Retry,
  Connect, scenario, or other device controls.
- The POS header shows only an aggregate such as Devices: Ready or Devices: 1 Attention;
  per-device detail belongs on the Status screen.
- The model remains suitable for real adapters, including customer-display states such as
  monitor unavailable, window open, and window closed without forcing Connected terminology.

### POS-710 Catalog and Cart Interaction Polish

Make product discovery and cart entry direct, filterable, and keyboard accessible.

Acceptance criteria:

- Product categories are derived/bound and selecting one filters the product grid while search
  and barcode paths continue to work.
- Clicking a product tile adds it to the cart; the redundant Add button is removed with keyboard
  and accessibility behavior preserved.
- Placeholder Hold Cart is removed unless a real hold/resume workflow is deliberately scoped.
- Product-picker and cashier category filtering share one category source.
- Focused tests cover category/search interaction, repeated tile activation, keyboard access,
  and cart quantities.

### POS-716 Navigation, Session, and Layout Polish

Add a safe sign-out lifecycle and resolve the remaining high-visibility navigation/layout issues.

Acceptance criteria:

- The POS header removes redundant status copy, aligns the API connectivity indicator, and keeps
  essential cashier/store/terminal context.
- Sign-out cancels pending device operations, stops scanner coordination, closes payment/receipt
  and customer-display windows, clears cart, checkout/session and receipt-preview state, clears
  CurrentSessionContext, and returns to login.
- Signing in again starts without state from the previous cashier session.
- Dashboard order rows size or wrap so order/status text is not clipped at supported window sizes.
- The affected POS, payment, receipt, navigation, and dashboard screens receive a consistent
  spacing/typography polish and focused ViewModel/UI tests.
- A lifecycle test covers cart contents, open customer display, pending device request, sign-out,
  cancellation/cleanup, login navigation, and clean re-login.

---

## EPIC-09 Production Readiness

Goal: Improve reliability, performance, test coverage, and portfolio presentation.

Status: Complete. Some sync hardening work was completed early under the historical
POS-711 through POS-715 IDs.

Scope boundary:

- Improve automated verification, configuration, recovery, diagnostics, performance,
  accessibility, and portfolio presentation.
- Keep real device SDKs, production identity, deployment packaging, refunds,
  cancellations, promotions, memberships, and a generic device framework out of scope.

### POS-801 Automated Quality and Test Baseline

Extend the EPIC-08 closeout CI into a clear repository quality gate.

Scope:

- Keep Windows Release restore, build, and full-solution tests required for pull requests.
- Add deterministic test categorization and a coverage artifact or summary without imposing
  an arbitrary percentage gate before a baseline is measured.
- Document required-check and local validation expectations.
- Keep CI free of machine-specific paths, secrets, and external service dependencies.

### POS-808 Configuration, Diagnostics, and Error Handling Hardening

Combine environment configuration, operational diagnostics, audit-safe logging, and
user-safe error boundary work that touches the same startup and composition roots.

Scope:

- Separate Development, Demo, and Production-safe configuration defaults.
- Validate API endpoint, local database, logging, scheduler, connectivity, and simulator options.
- Disable demo-only device controls and credentials outside explicit demo/development profiles.
- Strengthen startup and operational failure diagnostics without logging credentials, payment
  data, tokens, secrets, or internal exception details to users.

### POS-810 Offline, Recovery, and Restart Integration Scenarios

Combine integration-test and recovery-test work around the same durable SQLite workflows.

Scope:

- Exercise offline checkout, order persistence, sync queue creation, reconnect, retry, and
  duplicate-safe upload through real application/infrastructure boundaries.
- Exercise restart recovery for interrupted and Unknown payment states.
- Verify manager-review records remain durable and carts/orders are not duplicated or lost.
- Keep tests deterministic and independent of developer machine state.

### POS-803 Performance Test Data and Baselines

Add repeatable large catalog, cart, order history, recovery, and sync queue data sets and
record baseline timings for the operations that affect cashier responsiveness.

### POS-805 UI, DPI, and Accessibility Polish

Validate supported window sizes, keyboard focus, readable status semantics, long metadata,
per-monitor DPI behavior, and the documented device/session smoke paths.

### POS-806 Demo Guide and Portfolio Summary

Combine operator demo instructions and the architecture portfolio narrative so the documented
scenario, screenshots, limitations, and design decisions stay aligned with the shipped build.

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

## EPIC-10 Register Workflow Redesign

Objective: Redesign the cashier experience around barcode-first selling, a dominant
selected-product list, Register-owned payment controls, and persisted receipt history.

Status: Active.

Window policy:

- Register, Product Search, Receipt History, Receipt Detail, Recovery, Dashboard, and
  Status are MainWindow screens.
- Device Simulator remains a separate modeless operator utility window.
- Customer Display remains a dedicated device-output window.
- Cash tender and card authorization expand inline in Register; neither presents a
  second payment-method chooser or opens a payment dialog.
- Receipt workflow windows are removed after receipt history/detail is complete.

### POS-901 In-Window Workflow Navigation Foundation

Add a scoped typed workflow navigator with explicit transition policy, back history,
and push, replace, back, and root-reset behavior. Migrate existing Login, Register,
Recovery, Dashboard, Status, and sign-out transitions to the navigator without removing
current payment or receipt behavior.

Status: Completed.

### POS-902 Scan-First Register and Product Search Screens

Make the selected-product list the default Register workspace and move dense product
search into an on-demand in-window screen. Preserve only currently supported cart
actions; hold/resume remains out of scope.

Status: Completed.

### POS-903 Inline Credit Card Payment Workflow

Start card authorization from the Register bottom payment bar and show request/result
state in the inline bottom panel while preserving terminal cancellation, Unknown-result
recovery, duplicate prevention, and order completion semantics.

Status: In review via PR #168.

### POS-904 Inline Cash Tender and Change Workflow

Add decimal-safe cash received input, quick tender values, change calculation, and
under-tender validation in an inline Register bottom panel.

Status: In review via PR #168. Persistence of cash tendered and change remains tracked
by POS-907.

### POS-907 Persist Cash Tender and Change Metadata

Persist optional cash tendered and change amounts through payment, pending checkout,
order completion, local storage, sync payloads, and receipt projection. Keep card
payments unchanged and migrate existing SQLite databases without data loss.

### POS-905 Receipt History List and Detail

Add bounded persisted receipt summary/detail queries and in-window receipt history,
selection, print, and reprint workflows.

### POS-906 Retire Remaining Workflow Dialogs and Validate Cashier Journey

Keep the PaymentDialog retirement from POS-903/POS-904 intact, retire ReceiptDialog
after the receipt history/detail replacement ships, then validate lifecycle, sign-out,
accessibility, DPI, documentation, and end-to-end cashier paths.
