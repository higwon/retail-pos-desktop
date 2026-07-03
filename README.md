# Retail POS Desktop

A portfolio-oriented Windows desktop POS system built with C# and .NET.

The project focuses on offline-first retail sales workflows, local database management, server synchronization, peripheral device integration, customer-facing display simulation, and maintainable Windows application architecture.

## Project Goal

This project demonstrates professional Windows application engineering skills for retail/POS environments.

Key goals:

- Build a WPF-based desktop POS client using MVVM.
- Support offline sales processing with a local SQLite database.
- Synchronize local sales data with a central ASP.NET Core API.
- Model realistic retail business logic such as products, carts, orders, payments, discounts, stock, and receipts.
- Simulate Windows peripheral devices such as barcode scanners, receipt printers, card readers, and customer-facing displays.
- Keep the architecture clean, testable, and suitable for long-term maintenance.

## Recommended Reading Order

Codex or any AI coding agent should read the documents in this order before generating code:

1. [AI Instructions](docs/00_AI_INSTRUCTIONS.md)
2. [Project Specification](docs/01_PROJECT_SPEC.md)
3. [Requirements](docs/02_REQUIREMENTS.md)
4. [Architecture](docs/03_ARCHITECTURE.md)
5. [Database](docs/04_DATABASE.md)
6. [API Specification](docs/05_API_SPEC.md)
7. [UI Guidelines](docs/06_UI_GUIDELINES.md)
8. [Screen Flow](docs/07_SCREEN_FLOW.md)
9. [Roadmap](docs/08_ROADMAP.md)
10. [Coding Convention](docs/09_CODING_CONVENTION.md)
11. [Task Backlog](docs/10_TASK_BACKLOG.md)
12. [UI Design](docs/11_UI_DESIGN.md)
13. [UI Changelog](docs/12_UI_CHANGELOG.md)

## Figma UI Reference

Figma file:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

The Figma file is the primary UI reference for WPF implementation. Follow `docs/11_UI_DESIGN.md` before implementing UI screens.

## Target Technology Stack

- C#
- .NET 8
- WPF
- MVVM
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- SQLite for local offline storage
- SQL Server for central server storage
- ASP.NET Core Web API
- REST API
- Socket communication where useful
- Serilog or Microsoft.Extensions.Logging
- xUnit or NUnit for tests

## High-Level Solution Structure

```text
RetailPOS
├── docs
├── src
│   ├── RetailPOS.Desktop
│   ├── RetailPOS.Application
│   ├── RetailPOS.Domain
│   ├── RetailPOS.Infrastructure
│   ├── RetailPOS.Api
│   └── RetailPOS.Shared
└── tests
    ├── RetailPOS.Application.Tests
    └── RetailPOS.Domain.Tests
```

## Core Scenario

The cashier can continue selling products even when the network is unavailable.

1. Cashier logs in.
2. Product is scanned or searched.
3. Cart is updated.
4. Customer display is updated.
5. Discounts are applied.
6. A recoverable pending checkout is stored locally.
7. Payment is approved through a simulator.
8. The order is stored locally and the pending checkout is completed.
9. Receipt is printed through a simulator.
10. When the network returns, pending orders are synchronized with the server.

## Development Rule

Do not implement everything at once.
Build the project step by step, commit by commit, according to the roadmap and task backlog.
