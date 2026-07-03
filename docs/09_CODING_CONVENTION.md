# Coding Convention

## Language

- Use C#.
- Prefer clear and readable code.
- Avoid overly compact one-line implementations when readability suffers.

## Naming

### Projects

Use the `RetailPOS.*` prefix.

Examples:

- RetailPOS.Desktop
- RetailPOS.Domain
- RetailPOS.Application
- RetailPOS.Infrastructure
- RetailPOS.Api

Do not add a shared contracts project until the documentation explicitly introduces one.

### Classes

Use PascalCase.

Examples:

- CheckoutService
- ProductSearchService
- ReceiptPrinterSimulator
- OfflineSyncService

### Interfaces

Prefix with `I`.

Examples:

- IProductRepository
- IOrderRepository
- IReceiptPrinter
- ICardPaymentTerminal

### Async Methods

Async methods must end with `Async`.

Examples:

- SearchProductsAsync
- CompleteCheckoutAsync
- SynchronizePendingOrdersAsync

## MVVM

- ViewModel names end with `ViewModel`.
- View names end with `View` or `Window`.
- Commands end with `Command`.
- Observable properties should use CommunityToolkit.Mvvm where possible.

## Money and Time

- Use `decimal` for all monetary values.
- Use KRW and round to whole won with no fractional amount.
- Store timestamps in UTC and convert them to local time only for display.
- Keep store-local `BusinessDate` separate from UTC event timestamps.

## Error Handling

- Do not swallow exceptions silently.
- Log infrastructure errors.
- Convert technical errors into user-friendly UI messages.
- Do not crash the POS flow because sync failed.

## Logging

Log important events:

- Login success/failure
- Order completed
- Payment approved/failed
- Receipt printed/failed
- Sync started/completed/failed
- Device simulator errors

Do not log sensitive data:

- Passwords
- Tokens
- Payment card details

## Tests

Prioritize tests for:

- Cart total calculation
- Discount calculation
- Order completion rules
- Idempotency behavior
- Sync queue state transitions

## XAML

- Keep styles in ResourceDictionaries.
- Avoid duplicated inline styles.
- Keep code-behind minimal.
- Use commands instead of click handlers where reasonable.

## Git Commit Messages

Use conventional commit style.

Examples:

- `docs: add project architecture guide`
- `feat: add cart domain model`
- `fix: prevent duplicate sync order upload`
- `refactor: separate receipt generation service`
- `test: add checkout service tests`
