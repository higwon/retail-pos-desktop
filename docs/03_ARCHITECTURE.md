# Architecture

## Architecture Style

Use a practical Clean Architecture style suitable for a WPF desktop application and ASP.NET Core API.

The architecture should be simple enough to build, but clear enough to explain in interviews.

## Solution Structure

```text
src
├── RetailPOS.Desktop
├── RetailPOS.Application
├── RetailPOS.Domain
├── RetailPOS.Infrastructure
├── RetailPOS.Api
└── RetailPOS.Shared

tests
├── RetailPOS.Domain.Tests
└── RetailPOS.Application.Tests
```

## Project Responsibilities

### RetailPOS.Desktop

WPF client application.

Contains:

- Views
- ViewModels
- XAML resources
- Navigation
- Desktop startup
- DI composition root for the client

Must not contain core business rules.

### RetailPOS.Domain

Core business model and rules.

Contains:

- Entities
- Value objects
- Domain services
- Business rules
- Domain exceptions

Examples:

- Product
- Cart
- CartItem
- Order
- OrderLine
- Payment
- DiscountRule
- Receipt

### RetailPOS.Application

Application use cases.

Contains:

- Service interfaces
- Use case services
- DTOs
- Application-level validation
- Transaction orchestration

Examples:

- LoginService
- ProductSearchService
- CartService
- CheckoutService
- SyncService
- ReceiptService

### RetailPOS.Infrastructure

Technical implementations.

Contains:

- SQLite repositories
- SQL Server repositories
- API clients
- Device simulators
- Logging implementations
- Sync queue persistence

### RetailPOS.Api

ASP.NET Core Web API.

Contains:

- API controllers
- API authentication
- Server-side services
- SQL Server persistence
- Server sync endpoints

### RetailPOS.Shared

Shared contracts only when truly necessary.

Contains:

- Shared DTOs
- API request/response contracts
- Common enums

Avoid turning this into a dumping ground.

## Dependency Direction

```text
RetailPOS.Desktop
|- RetailPOS.Application
|- RetailPOS.Domain
`- RetailPOS.Infrastructure

RetailPOS.Api
|- RetailPOS.Application
|- RetailPOS.Domain
`- RetailPOS.Infrastructure

RetailPOS.Infrastructure
|- RetailPOS.Application
`- RetailPOS.Domain

RetailPOS.Application
`- RetailPOS.Domain

RetailPOS.Domain
`- no project dependencies
```

`RetailPOS.Desktop` and `RetailPOS.Api` are executable composition roots. Each executable project registers the application services and the infrastructure implementations it needs in its own DI setup. Infrastructure must not own application startup or the root service provider.

## MVVM Rules

- ViewModel must not reference View.
- ViewModel exposes state and commands only.
- View code-behind should be minimal.
- UI events should be routed to commands where practical.
- Services should be injected into ViewModels.

## Device Integration Strategy

All device features should be behind interfaces.

Examples:

- IBarcodeScanner
- IReceiptPrinter
- ICardPaymentTerminal

Initial implementation should use simulators.
Real hardware can be added later without changing business logic.

## Offline-First Strategy

Checkout flow must save the order locally first.

Recommended flow:

```text
Complete Payment
↓
Create Order
↓
Save Order to SQLite
↓
Add Sync Queue Item
↓
Update UI
↓
Background Sync Attempts
```

## Recoverable Checkout Strategy

Before requesting payment approval, the application saves a `PendingCheckout`. After approval, it records the approved result, creates and saves the order, adds the sync queue item, and only then marks the pending checkout completed.

If the application restarts with a pending checkout in `ApprovedButOrderNotCreated`, it routes the user to checkout recovery. Recovery recreates the order idempotently or leaves it for manager review.

## Error Handling Strategy

- Domain errors should use clear domain exceptions or result objects.
- Infrastructure errors should be logged and translated into application errors.
- UI should show user-friendly messages.
- Sync failures should not crash the app.
