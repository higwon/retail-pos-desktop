# Architecture

## Style

Use a practical Clean Architecture style that is simple enough for a portfolio project and clear enough to explain during review.

## Projects

```text
src
|- RetailPOS.Desktop
|- RetailPOS.Application
|- RetailPOS.Domain
|- RetailPOS.Infrastructure
`- RetailPOS.Api
```

## Responsibilities

`RetailPOS.Domain`

- Entities, value objects, domain rules, and domain validations.
- No project dependencies.

`RetailPOS.Application`

- Use cases, service interfaces, DTOs, orchestration, and application validation.
- Depends on Domain.

`RetailPOS.Infrastructure`

- SQLite persistence, EF Core mappings, API clients, device simulators, sync implementations, and technical adapters.
- Depends on Application and Domain.

`RetailPOS.Desktop`

- WPF views, view models, navigation, desktop hosted services, UI composition root, and desktop DI registration.
- Depends on Application, Domain, and Infrastructure.

`RetailPOS.Api`

- ASP.NET Core endpoints, middleware, API composition root, request/response contracts, and server-side in-memory MVP stores.
- Depends on Application, Domain, and Infrastructure where needed.

## Dependency Direction

```text
Desktop -> Application, Domain, Infrastructure
Api -> Application, Domain, Infrastructure
Infrastructure -> Application, Domain
Application -> Domain
Domain -> none
```

Executable projects own DI registration. Infrastructure must not own application startup or the root service provider.

## MVVM Rules

- ViewModels expose state and commands.
- ViewModels must not reference Views.
- Views keep code-behind minimal.
- UI events should route to commands where practical.
- Services are injected into ViewModels.
- `ObservableObject` from CommunityToolkit.Mvvm is the default ViewModel base unless a stronger local abstraction is needed.
- ViewModels that subscribe to events, messenger messages, timers, or long-lived services must implement `IDisposable`.
- Do not dispose a ViewModel from WPF `Unloaded`; `Unloaded` can happen during visual tree detach or navigation reuse. Dispose through the owning DI scope, window/dialog close, or a deliberate navigation lifecycle hook.

## Messenger Rules

- Use `IMessenger` for cross-ViewModel or background-service UI refresh notifications.
- Register recipients in the ViewModel constructor or initialization path.
- Unregister in `Dispose`.
- Keep message payloads small and application-safe.
- Do not use messenger messages to hide core application flow. Important use cases still belong in Application services.

## Error Handling

- Domain errors should be explicit domain validations or exceptions.
- Infrastructure errors should be logged and translated at the application/UI boundary.
- UI-facing messages must be user-safe.
- Sync failures should not crash the desktop app.
- API unhandled errors should return ProblemDetails and hide technical details outside development.

## Device Strategy

Device integrations are behind interfaces and begin as simulators:

- Barcode scanner.
- Receipt printer.
- Card payment terminal.
- Customer-facing display.

Real hardware can be added later without changing core business logic.

Business-facing device ports expose normal operations only. Application use cases may
request a barcode scan result, receipt print, or payment authorization, but they must
not select simulator-only scenarios such as timeout, paper-out, or communication
failure. Simulator controls belong to Infrastructure or to a Desktop-only developer
surface and are registered separately from the business port.

Simulator implementations live in Infrastructure unless the behavior directly owns
Windows or WPF resources. Monitor discovery, WPF window placement, fullscreen display,
and DPI handling stay in Desktop.

Terminal-owned device services use one instance for the terminal UI scope. Long-running
device operations accept cancellation tokens. Device callbacks may arrive on a
background thread; Desktop adapters or coordinators must marshal UI-bound updates to
the WPF dispatcher before changing observable UI state.

Use these common connection terms where they apply:

- `Disconnected`
- `Connecting`
- `Connected`
- `Faulted`

Operational states such as `Busy`, `WaitingForCard`, or `Printing` remain
device-specific. Do not introduce a generic `IDevice<T>` framework until repeated
implementation demonstrates a useful shared contract.

Barcode scanning uses an event-producing device boundary for simulator and future
adapter integration. Existing TextBox and keyboard-wedge entry remains available as
manual input and fallback.

Customer display data continues to come from checkout and display state. Desktop owns
the display host, available-monitor discovery, single-window lifetime, target
selection, placement, disconnect fallback, and cleanup.
