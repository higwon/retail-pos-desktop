# Retail POS Desktop

[![CI](https://github.com/higwon/retail-pos-desktop/actions/workflows/ci.yml/badge.svg)](https://github.com/higwon/retail-pos-desktop/actions/workflows/ci.yml)

A portfolio-oriented Windows desktop POS system built with C# and .NET 8.

The project focuses on offline-first retail sales, recoverable checkout, local SQLite persistence, central API synchronization, simulated POS devices, and maintainable WPF architecture.

![Retail POS register with the modeless device simulator](docs/images/demo-device-simulator.png)

The screenshot shows the cashier register and the modeless Device Simulator running
together. Scanner, printer, card-terminal, and customer-display behavior can be exercised
without coupling simulator controls to cashier business commands.

<table>
  <tr>
    <td width="50%"><img src="docs/images/demo-login.png" alt="Retail POS login screen" /></td>
    <td width="50%"><img src="docs/images/demo-receipts.png" alt="Retail POS receipt history and selected detail" /></td>
  </tr>
  <tr>
    <td align="center">Demo cashier/manager sign-in</td>
    <td align="center">Persisted receipt history, detail, and reprint</td>
  </tr>
</table>

## At a Glance

| Area | Implementation |
| --- | --- |
| Client | Windows WPF, MVVM, CommunityToolkit.Mvvm |
| Offline operation | Local SQLite through EF Core migrations and repositories |
| Safe checkout | `PendingCheckout` is persisted before payment authorization |
| Device integration | Operator-driven scanner, printer, card-terminal, and customer-display simulators |
| Synchronization | Background upload, reconnect trigger, retry/exhausted states, stable idempotency identity |
| API | ASP.NET Core minimal API with health, product/order contracts, ProblemDetails, and request logging |
| Verification | Automated tests across five test projects with required Windows CI and coverage artifacts |

## Key Engineering Highlights

- **Fail-closed payment recovery:** timeout and communication loss become `Unknown` manager
  review states instead of guessed approvals or declines.
- **Durable offline checkout:** local order completion does not depend on API availability;
  retry due times and idempotency identity survive restart in SQLite.
- **Production-shaped device boundaries:** cashier commands depend on business ports while
  simulator scenario controls stay in Infrastructure/Desktop-only surfaces.
- **Explicit lifecycle and concurrency rules:** terminal transitions are single-winner,
  scanner callbacks are dispatcher-safe, and sign-out tears down pending session work.

## System Design

The solution follows a practical Clean Architecture dependency direction. Business rules
remain independent of WPF, SQLite, HTTP, and simulator implementations.

```mermaid
flowchart TB
    subgraph Client["Windows terminal / offline-first client"]
        Desktop["RetailPOS.Desktop<br/>WPF · MVVM · navigation · session lifetime"]

        subgraph Core["Business core"]
            Application["RetailPOS.Application<br/>checkout · recovery · sync · device ports"]
            Domain["RetailPOS.Domain<br/>product · cart · order · payment rules"]
            Application --> Domain
        end

        Infrastructure["RetailPOS.Infrastructure<br/>EF Core · HTTP clients · device adapters"]
        SQLite[("Local SQLite<br/>product cache · pending checkout<br/>orders · sync queue")]
        Devices["Device simulators<br/>scanner · printer · card terminal"]
        Display["Customer display<br/>secondary-monitor window"]

        Desktop --> Application
        Desktop --> Infrastructure
        Infrastructure --> Domain
        Infrastructure -.->|"implements ports"| Application
        Infrastructure --> SQLite
        Infrastructure --> Devices
        Desktop --> Display
    end

    subgraph Server["Central service boundary"]
        Api["RetailPOS.Api<br/>health · product sync · order upload<br/>ProblemDetails · request logging"]
        ApiStore[("Demo server stores<br/>order idempotency state")]
        Api --> ApiStore
    end

    Infrastructure -->|"HTTP: health, products, orders"| Api
```

Solid arrows inside the client are compile-time dependencies or owned adapters. The dashed
arrow means Infrastructure implements interfaces defined by Application. The HTTP arrow is
the runtime boundary between the offline-capable terminal and the central API.

Project responsibilities:

- **Domain** owns business entities and invariants and has no project dependencies.
- **Application** owns checkout, recovery, sync, authentication, receipt, and device ports.
- **Infrastructure** implements SQLite persistence, HTTP clients, and device simulators.
- **Desktop** owns WPF presentation, terminal session lifetime, navigation, and composition.
- **API** exposes the central health/product/order boundaries and idempotent upload behavior.

The most important runtime path is durable before it is connected:

```mermaid
flowchart LR
    Cart["Cart confirmed"] --> Pending["Persist PendingCheckout"]
    Pending --> Payment["Request payment"]
    Payment --> Approved{"Approved?"}
    Approved -->|"Yes"| Local["Save order + sync queue"]
    Local --> Receipt["Show/print receipt"]
    Local --> Upload["Background API upload"]
    Approved -->|"Unknown"| Review["Keep cart and require review"]
    Upload -->|"Offline/failure"| Retry["Persist retry due time"]
    Retry --> Upload
```

## How It Works

The cashier and device operator can use the POS and Simulator at the same time. A normal
card sale runs through the following production-shaped boundaries:

```mermaid
sequenceDiagram
    actor Cashier
    participant POS as WPF Desktop
    participant DB as Local SQLite
    participant Terminal as Card Terminal Simulator
    participant API as ASP.NET Core API

    Cashier->>POS: Sign in and add products
    POS->>POS: Update cart and customer display
    Cashier->>POS: Choose Credit Card in Register
    POS->>DB: Save PendingCheckout
    POS->>Terminal: Create authorization request
    Note over POS,Terminal: Inline payment status remains visible while Simulator stays usable
    Terminal-->>POS: Approve / Decline / Unknown
    alt Approved
        POS->>DB: Save order and SyncQueue item atomically
        POS->>DB: Mark PendingCheckout completed
        POS-->>Cashier: Open persisted receipt detail in MainWindow
        POS->>API: Upload order when online
        API-->>POS: Completed or idempotent existing result
        POS->>DB: Mark SyncQueue completed
    else Unknown or communication loss
        POS->>DB: Preserve manager-review state
        POS-->>Cashier: Keep cart and block silent retry
    else Declined
        POS-->>Cashier: Show safe failure and keep checkout recoverable
    end
```

Offline operation uses the same checkout path; only the upload is deferred:

1. Products, the pending checkout, completed order, and sync queue live in local SQLite.
2. If the API is unavailable, checkout still completes locally and the queue stores its next
   retry time.
3. The connectivity monitor detects recovery and triggers a bounded synchronization run.
4. A stable `storeId + terminalId + localOrderId` identity prevents duplicate server orders.
5. After restart, interrupted or Unknown payments are routed to Recovery instead of being
   guessed as approved or declined.

The other simulated devices follow the same separation:

- **Barcode Scanner:** the operator selects a product or enters a raw barcode; the scanner
  raises an event that is marshalled to the WPF dispatcher before the cart changes.
- **Receipt Printer:** Print creates a pending request containing safe receipt data; the
  operator responds Printed, Paper out, Cover open, Timeout, or another typed result.
- **Customer Display:** cart/payment state is shared with a Desktop-owned window that moves
  between secondary monitors without creating duplicate display windows.
- **Sign out:** pending payment and print work is cancelled, scanner coordination stops,
  Simulator and Customer Display close, and cart, receipt, checkout, and cashier session
  state are cleared.

For a clean-checkout walkthrough, screenshots, explicit limitations, and links to concrete
code/tests, see the [Demo Guide and Portfolio Summary](docs/demo-and-portfolio.md).

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

## Development Process

The project was developed through an issue-driven workflow inspired by Scrum. Each feature
started as a scoped GitHub issue with explicit acceptance criteria.

AI tools (ChatGPT and Codex) were used to accelerate design discussions, implementation,
documentation, and code review. They operated within the project workflow; they did not own
the product or its engineering decisions.

The project owner remained responsible for:

- product decisions;
- architecture and technical trade-offs;
- review feedback and requested changes;
- acceptance against the issue criteria;
- final code ownership and merge decisions.

```mermaid
flowchart TB
    Backlog["Backlog and priorities<br/>Project owner"]
    Issue["GitHub Issue<br/>scope and acceptance criteria"]
    Design["Architecture discussion<br/>Project owner + ChatGPT"]
    Implement["Focused implementation<br/>Codex"]
    Review["Review and change requests<br/>Project owner + ChatGPT"]
    CI["Required CI<br/>build · automated tests · coverage"]
    Merge["Acceptance and merge<br/>Project owner"]

    Backlog --> Issue
    Issue --> Design
    Design --> Implement
    Implement --> Review
    Review -->|"changes requested"| Implement
    Review -->|"accepted"| CI
    CI -->|"green"| Merge
    Merge -->|"next increment"| Backlog
```

This made AI usage repeatable and auditable rather than ad hoc. GitHub history preserves the
issue scope, acceptance criteria, review-driven corrections, CI evidence, and author-approved
merge for each significant increment.

## Development Rule

Build the project step by step, issue by issue. Keep each PR focused, update docs when project rules change, and keep code behavior aligned with the current source-of-truth documents.
