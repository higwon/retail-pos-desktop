# UI Guidelines

## UI Direction

The UI should look like a professional Windows retail POS application.

It should not look like a generic admin CRUD page.
It should prioritize cashier speed, readability, and operational clarity.

## Design Keywords

- Modern desktop POS
- Cashier-first
- Keyboard-first
- Touch-friendly
- Clear totals
- Large action buttons
- Minimal visual noise
- Professional business software

## Main POS Layout

Recommended main screen structure:

```text
┌─────────────────────────────────────────────────────────────┐
│ Header: Store / Cashier / Network / Sync / Time              │
├───────────────┬─────────────────────────────┬───────────────┤
│ Categories    │ Product Search / Grid        │ Cart          │
│               │                             │               │
│ - All         │ [Search or barcode input]    │ Items         │
│ - Drinks      │                             │ Qty / Price   │
│ - Beauty      │ Product Cards                │ Discounts     │
│ - Health      │                             │ Total         │
│               │                             │               │
├───────────────┴─────────────────────────────┴───────────────┤
│ Bottom: Hold / Cancel / Discount / Cash / Card / Checkout    │
└─────────────────────────────────────────────────────────────┘
```

## Important Screens

### Login Screen

- Employee code
- Password
- Login button
- Offline mode indicator if server is unavailable

### POS Main Screen

- Barcode/search input should receive focus by default.
- Cart should always be visible.
- Total amount should be visually dominant.
- Payment buttons should be easy to access.

### Payment Dialog

- Total amount
- Payment method
- Simulated approval result
- Confirm/cancel actions

### Receipt Dialog

- Receipt preview
- Print button
- Reprint option

### Sync Status Screen

- Pending sync count
- Failed sync count
- Retry button
- Last sync time

### Admin Dashboard

- Daily sales
- Order count
- Failed sync count
- Product/stock summary

## UX Rules

- Avoid blocking UI during API calls.
- Show loading states for long operations.
- Show clear error messages.
- Make offline mode visible.
- Make failed sync visible but not disruptive during checkout.
- Use keyboard shortcuts for frequent actions.

## Suggested Keyboard Shortcuts

- `F2`: Focus product search
- `F4`: Quantity change
- `F6`: Apply discount
- `F8`: Card payment
- `F9`: Cash payment
- `F12`: Complete checkout
- `Esc`: Cancel dialog or current operation

## Visual Style

- Use a clean light theme first.
- Use consistent spacing.
- Use simple icon placeholders if needed.
- Avoid brand-specific colors.
- Avoid excessive gradients or animations.

## WPF Implementation Notes

- Use ResourceDictionaries for styles.
- Use reusable user controls for product cards, cart items, and status indicators.
- Avoid large code-behind logic.
- Use binding and commands.
