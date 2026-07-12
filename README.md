# Retail POS Desktop

[![CI](https://github.com/higwon/retail-pos-desktop/actions/workflows/ci.yml/badge.svg)](https://github.com/higwon/retail-pos-desktop/actions/workflows/ci.yml)

A portfolio-oriented Windows desktop POS system built with C# and .NET 8.

The project focuses on offline-first retail sales, recoverable checkout, local SQLite persistence, central API synchronization, simulated POS devices, and maintainable WPF architecture.

## Current Scope

- WPF desktop client using MVVM and CommunityToolkit.Mvvm.
- Local SQLite persistence through EF Core.
- Recoverable checkout using `PendingCheckout` before payment approval.
- Order upload synchronization with retry, idempotency, background scheduling, connectivity monitoring, and status refresh.
- ASP.NET Core API skeleton with health, product sync, order upload, ProblemDetails, request logging, and idempotency handling.
- Simulated UI flows for login, POS main, payment, receipt, checkout recovery, customer display, dashboard, and sync status.

## Documentation

Start here:

1. [Project Overview](docs/project-overview.md)
2. [Architecture](docs/architecture.md)
3. [Roadmap](docs/roadmap.md)
4. [Demo Guide and Portfolio Summary](docs/demo-and-portfolio.md)

For implementation planning:

- [Epics and Tasks](docs/epics-and-tasks.md)
- [Development Workflow](docs/development-workflow.md)
- [Repository Agent Guide](docs/agent-guide.md)

For specific areas:

- [API Contracts](docs/api-contracts.md)
- [Sync and Offline](docs/sync-and-offline.md)
- [UI Guide](docs/ui-guide.md)
- [Decisions](docs/decisions.md)
- [Demo Guide and Portfolio Summary](docs/demo-and-portfolio.md)

AI coding agents should also read [AGENTS.md](AGENTS.md) before working in this repository.

## Figma UI Reference

Figma file:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

The Figma file is the primary UI reference for WPF screen implementation. Use [UI Guide](docs/ui-guide.md) for repository-specific mapping notes.

## Technology Stack

- C# and .NET 8
- WPF
- MVVM
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting and DependencyInjection
- EF Core with SQLite for local offline storage
- ASP.NET Core Web API
- Serilog for desktop structured logging
- xUnit tests

## Solution Structure

```text
RetailPOS
|- AGENTS.md
|- docs
|- src
|  |- RetailPOS.Desktop
|  |- RetailPOS.Application
|  |- RetailPOS.Domain
|  |- RetailPOS.Infrastructure
|  `- RetailPOS.Api
`- tests
   |- RetailPOS.Api.Tests
   |- RetailPOS.Application.Tests
   |- RetailPOS.Desktop.Tests
   |- RetailPOS.Domain.Tests
   `- RetailPOS.Infrastructure.Tests
```

## Core Scenario

The cashier can keep selling products even when the API is unavailable.

1. Cashier logs in.
2. Product is scanned or searched.
3. Cart and customer display update.
4. Manual discount is applied if needed.
5. `PendingCheckout` is stored locally.
6. Payment is approved through a simulator.
7. Order is stored locally and queued for sync.
8. Receipt is shown through the simulator flow.
9. Background sync uploads pending orders when the API is reachable.

## Development Rule

Build the project step by step, issue by issue. Keep each PR focused, update docs when project rules change, and keep code behavior aligned with the current source-of-truth documents.
