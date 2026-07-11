# Development Workflow

## Branching

- Work from the latest `main`.
- Use focused branches for each issue or documentation cleanup.
- Keep PRs small enough to review comfortably.
- Draft PRs are preferred while work is still being validated.

## Before Changing Files

Run:

```powershell
git status --short --branch
```

Check related docs and existing local patterns before implementing.

## Common Commands

Run all tests:

```powershell
dotnet test RetailPOS.sln
```

Run the CI-equivalent Release validation:

```powershell
dotnet restore RetailPOS.sln
dotnet build RetailPOS.sln -c Release --no-restore
dotnet test RetailPOS.sln -c Release --no-build --no-restore --collect "XPlat Code Coverage"
git diff --check
```

Tests are categorized by project boundary: Domain, Application, Infrastructure, Desktop,
and API. CI publishes TRX results and Cobertura coverage files for all five projects as a
single retained artifact. Coverage is evidence for identifying gaps; no percentage gate is
enforced until a stable baseline and meaningful exclusions are agreed.

Repository settings should require the `build-and-test` check before merging to `main`.

Run Desktop tests:

```powershell
dotnet test tests\RetailPOS.Desktop.Tests\RetailPOS.Desktop.Tests.csproj
```

Run API tests:

```powershell
dotnet test tests\RetailPOS.Api.Tests\RetailPOS.Api.Tests.csproj
```

Run Application tests:

```powershell
dotnet test tests\RetailPOS.Application.Tests\RetailPOS.Application.Tests.csproj
```

## Local Configuration

Desktop configuration:

```text
src/RetailPOS.Desktop/appsettings.json
```

API configuration:

```text
src/RetailPOS.Api/appsettings.json
src/RetailPOS.Api/appsettings.Development.json
```

Default Desktop API base address:

```text
http://localhost:5000/
```

Default local database path:

```text
%LOCALAPPDATA%\RetailPOS\retail-pos.db
```

Default desktop log path:

```text
%LOCALAPPDATA%\RetailPOS\logs
```

MVP demo login accounts:

```text
Cashier: E0001 / 1234
Manager: M0001 / 1234
```

Cashier happy path demo checklist:

1. Sign in with the cashier demo account.
2. Add one product by barcode and another through product search.
3. Apply a fixed discount and confirm the cart total changes.
4. Start checkout and approve a card payment.
5. Confirm the receipt preview shows the completed order and discount.
6. Open sync status and confirm the order is queued while the API is offline.

Device and session smoke checklist:

1. Open the Device Simulator and confirm scanner, printer, card terminal, and customer
   display status is visible without blocking the cashier window.
2. Select a product in the scanner picker, emit it repeatedly, and confirm cart quantity
   changes while manual unknown-barcode entry remains available.
3. Start receipt printing, inspect the request payload, respond with a failure, retry, and
   respond Printed; confirm feedback stays in the receipt header area.
4. Start card payment and respond Approve from the simulator; confirm approval metadata does
   not clip and the order completes only once.
5. Move the customer display between secondary monitors, then disconnect the selected target
   and confirm the POS remains usable with an attention state.
6. Start a delayed card request and select Sign out while it is pending; confirm the request
   is cancelled, scanner input stops, workflow windows and customer display close, and login
   is shown.
7. Sign in as a different cashier and confirm the previous cart, receipt, checkout, and
   cashier context are absent.

## PR Description

Include:

- Summary.
- What changed.
- Why it changed.
- User or developer impact.
- Validation commands and results.
- Related issue.

## Documentation Rule

The active documentation set is:

- `README.md`
- `AGENTS.md`
- `docs/project-overview.md`
- `docs/architecture.md`
- `docs/decisions.md`
- `docs/api-contracts.md`
- `docs/sync-and-offline.md`
- `docs/ui-guide.md`
- `docs/roadmap.md`
- `docs/epics-and-tasks.md`
- `docs/development-workflow.md`
- `docs/agent-guide.md`

Do not add a new markdown file unless it has a distinct long-term purpose. Prefer updating one of the source-of-truth files above.

## Issue Planning Rule

`docs/epics-and-tasks.md` is the implementation scope source of truth. Use it to create GitHub Issues, choose the next task, and keep PRs aligned with the intended epic sequence.

`docs/roadmap.md` is only the phase-level summary. GitHub Issues are the operational task tracker derived from `docs/epics-and-tasks.md`.
