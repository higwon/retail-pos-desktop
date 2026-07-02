# Requirements

## Functional Requirements

### Authentication

- Cashier can log in with employee ID and password.
- The desktop app stores only the minimum required session state.
- API authentication should use JWT after login.

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

### Discount Rules

Initial rules:

- Fixed amount discount
- Percentage discount
- Product-level discount
- Cart-level discount

Future rules:

- Buy N get M
- Membership discount
- Coupon code
- Time-limited promotion

### Payment

- Cash payment simulation
- Card payment simulation
- Payment approval or failure result
- Failed payment should not complete the order

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
- Server stock synchronization can be improved in later phases.

## Non-Functional Requirements

### Reliability

- The app should not lose completed orders during network failure.
- Local persistence must happen before marking an order as completed.

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

## Suggested Sample Data

- 100 to 1,000 products for MVP.
- 10,000+ orders for performance testing later.
- Multiple product categories.
- Several discount rule examples.
