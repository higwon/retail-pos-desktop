# Epics and Task Breakdown

This document defines the Jira-style task structure for the Retail POS Desktop project.

All implementation work should be tracked by a `POS-XXX` task ID.

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

Configure project references according to `docs/03_ARCHITECTURE.md`.

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
- Theme values align with `docs/11_UI_DESIGN.md`.
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

Status: Todo.

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

### POS-503 Desktop Product Sync Client and Upsert

### POS-504 Order Upload API Contract

### POS-505 API Idempotency Handling for Order Upload

### POS-506 Desktop Order Sync Worker and Retry Policy

### POS-507 Sync Status UI Integration

### POS-508 Align Local Order Sync Queue Payload with Upload Contract

Expand the local order sync queue payload so it contains the full completed-order data
required by the API upload contract before the Desktop sync worker consumes it.

---

## EPIC-07 Device Simulation

Goal: Demonstrate Windows POS peripheral integration concepts.

### POS-601 Barcode Scanner Simulator

### POS-602 Receipt Printer Simulator

### POS-603 Card Reader Simulator

### POS-604 Secondary Monitor Customer Display

---

## EPIC-08 Production Readiness

Goal: Improve reliability, performance, test coverage, and portfolio presentation.

Run this hardening phase after EPIC-06 API and Synchronization and before expanding
device/peripheral scope too far. These tasks improve operational quality rather than
adding major cashier-facing features.

### POS-701 Unit Tests

### POS-702 Integration Tests

### POS-703 Performance Test Data

### POS-704 Error Handling Polish

### POS-705 UI Polish

### POS-706 Demo Guide

### POS-707 Portfolio Summary

### POS-708 Configuration and Environment Hardening

Review appsettings, local development defaults, API/Desktop endpoint configuration,
and machine-specific paths.

### POS-709 Logging and Audit Hardening

Strengthen operational logging, audit-relevant events, and safe diagnostic context
without logging sensitive data.

### POS-710 Offline and Recovery Scenario Tests

Exercise offline/online transitions, interrupted checkout recovery, sync retries, and
large product/order scenarios.
