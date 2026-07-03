# Requirements

## Functional Requirements

### Authentication

- Cashier can log in with employee ID and password.
- The desktop app stores only the minimum required session state.
- API authentication should use JWT after login.
- The first login on a terminal must be completed online.
- After a successful online login, the minimum employee authentication data required for offline login is cached locally.
- Offline login is limited to previously cached employees and is valid for 7 days from their last successful online authentication.
- Offline permissions use the role and permissions from the most recent successful synchronization.
- Expired or missing cached credentials must not allow offline login.

### Product Search

- Cashier can search products by name, barcode, or SKU.
- Product results should show name, price, barcode, stock, and category.
- Barcode input should be treated as a fast path for product lookup.

### Cart

- Cashier can add products to the cart.
- Cashier can change quantity.
- Cashier can remove cart items.
- Cart total updates immediately.
- Cart should support discounts and taxes later.

### Customer Display

- The system should provide a customer-facing display simulator in the MVP.
- The customer display shows the current cart items, quantities, discounts, and total amount.
- The customer display updates whenever the cashier changes the cart.
- During payment, the customer display shows the amount to pay and payment waiting status.
- After payment approval, the customer display shows a thank-you message and receipt guidance.
- The MVP implementation may use a separate WPF window on the same monitor.
- Phase 2 can support real secondary-monitor placement and display selection.

### Discount Rules

MVP rules:

- Fixed amount discount
- Percentage discount

Phase 2 rules:

- Coupon discount
- Promotional discount
- Membership discount
- Discount rule engine

### Payment

- Cash payment simulation
- Card payment simulation
- Payment approval or failure result
- Failed payment should not complete the order
- A `PendingCheckout` must be persisted before requesting payment approval.
- After approval, the order must be saved before the pending checkout is marked completed.
- If payment is approved but order creation does not finish, the checkout must remain recoverable after application restart.

### Receipt

- Generate receipt text after order completion.
- Print receipt through a simulator first.
- ESC/POS-style receipt formatting can be added later.

### Offline Mode

- The app must allow order completion even when the API is unavailable.
- Completed local orders are added to a pending sync queue.
- The sync queue retries when the network becomes available.

### Synchronization

- Local orders are synchronized to the server.
- Sync status is tracked per order.
- Failed sync attempts are logged.
- Duplicate server order creation should be prevented by idempotency keys.

### Sales History

- Cashier or manager can view completed orders.
- Sync status should be visible.
- Failed orders should be easy to identify.

### Stock

- Stock decreases after order completion.
- Local stock should be updated immediately.
- Server stock is the authoritative stock value.
- Local stock is an estimated display value that accounts for locally completed, unsynchronized orders.
- Each unsynchronized order represents a pending stock deduction.
- After successful order synchronization, the server deducts stock and the client refreshes server stock.
- Product synchronization must not overwrite pending local deductions. Local estimated stock is calculated from synchronized server stock minus pending deduction quantities.

### Checkout Recovery

- Application startup must detect pending checkouts in an `ApprovedButOrderNotCreated` state.
- The user must be shown a recovery screen when such records exist.
- Recovery supports recreating the order or requesting manager review.
- Recovery actions must be idempotent and must not create duplicate orders.

## Non-Functional Requirements

### Reliability

- The app should not lose completed orders during network failure.
- Local persistence must happen before marking an order as completed.
- Checkout recovery state must survive process termination and machine restart.

### Performance

- Product search should feel instant for normal retail datasets.
- The UI must not freeze during synchronization or API calls.
- Long-running work must use async operations or background services.

### Maintainability

- Business logic must be testable without WPF.
- Device integrations should be behind interfaces.
- API and local DB implementations should be replaceable.

### Security

- Do not commit secrets.
- Do not store plain text passwords.
- Use configuration files or user secrets for development secrets.

### Money and Time

- Monetary values use the .NET `decimal` type.
- The MVP currency is KRW.
- Amounts are rounded to whole won and do not retain fractional values.
- Persist timestamps in UTC.
- Convert timestamps to the local time zone for display.

### Retry Policy

- Synchronization retries use exponential backoff.
- Automatic retry is limited to 5 attempts.
- After 5 failed attempts, the item remains visible for manual review or retry.

### MVP Scope Exclusions

- Refund and order cancellation workflows are excluded from the MVP.
- Refund and cancellation are planned for Phase 2.

## Suggested Sample Data

- 100 to 1,000 products for MVP.
- 10,000+ orders for performance testing later.
- Multiple product categories.
- Several discount rule examples.
