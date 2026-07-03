# Coding Standards

This document defines coding standards for the Retail POS Desktop project.

Codex and any coding agent must follow these rules unless a task explicitly says otherwise.

## General Principles

- Prefer readability over cleverness.
- Keep changes small and scoped to the current task.
- Do not implement future tasks early.
- Do not introduce technology only for its own sake.
- Keep business logic testable without WPF.
- Do not commit secrets, connection strings, passwords, tokens, or private keys.

## Language and Runtime

- Use C#.
- Target .NET 8 unless the task or project file states otherwise.
- Use nullable reference types where practical.
- Prefer explicit, descriptive names.

## Architecture Rules

- `RetailPOS.Domain` must not reference other project layers.
- `RetailPOS.Application` may reference `RetailPOS.Domain`.
- `RetailPOS.Infrastructure` may reference `RetailPOS.Application` and `RetailPOS.Domain`.
- `RetailPOS.Desktop` is a composition root for the WPF client.
- `RetailPOS.Api` is a composition root for the server API.
- Do not place business rules in WPF Views or code-behind.
- Do not place persistence logic in Domain.

## MVVM Rules

- Views must not contain business logic.
- ViewModels must not reference View classes.
- Prefer `CommunityToolkit.Mvvm` for observable properties and commands.
- Use commands for user actions.
- Use services/interfaces for navigation, dialogs, device simulation, and persistence.
- Avoid direct static service access from ViewModels.

## Dependency Injection

- Register dependencies in the composition root.
- Do not use a static service locator.
- Prefer constructor injection.
- Keep service lifetimes intentional and easy to explain.

## Async and Threading

- Use `async`/`await` for I/O and long-running work.
- Do not block the UI thread with `.Result`, `.Wait()`, or long synchronous operations.
- Use `CancellationToken` for operations that may take time or be cancelled.
- UI updates must occur on the UI thread.

## Error Handling

- Do not silently swallow exceptions.
- Log unexpected exceptions.
- Use user-friendly messages in UI boundaries.
- Keep technical exception details out of customer-facing screens.

## Logging

- Use `ILogger` or the logging abstraction selected by the project.
- Log important application lifecycle events.
- Log checkout recovery and sync failure scenarios.
- Do not log passwords, tokens, card numbers, or sensitive customer data.

## Money and Time

- Use `decimal` for monetary values.
- MVP currency is KRW.
- Do not use floating-point types for money.
- Persist timestamps in UTC.
- Convert timestamps to local time only for display.

## Naming

Recommended naming:

```text
Views:          LoginView, PosMainView, CustomerDisplayWindow
ViewModels:     LoginViewModel, PosMainViewModel, CustomerDisplayViewModel
Services:       ICheckoutService, CheckoutService
Repositories:   IOrderRepository, SqliteOrderRepository
DTOs:           OrderUploadRequest, OrderUploadResponse
Commands:       SignInCommand, CheckoutCommand
```

## XAML Rules

- Prefer ResourceDictionary styles for reusable visual rules.
- Avoid duplicating colors and spacing directly in many controls.
- Keep large views readable by extracting UserControls when needed.
- Do not over-optimize UI before the shell and behavior are stable.
- Follow the Figma reference in `docs/11_UI_DESIGN.md` for layout direction.

## Test Rules

- Domain logic should have unit tests.
- Checkout and recovery logic should be tested before being considered complete.
- Tests should avoid WPF dependencies where possible.
- Prefer small focused tests over large fragile tests.

## Documentation Rules

Update documentation when:

- Architecture changes.
- Public API contracts change.
- Database schema changes.
- Task scope changes.
- Figma/UI behavior changes meaningfully.
- A new architectural decision is made.
