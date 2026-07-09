# UI Guide

## Primary Reference

Figma:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

Use the Figma file as the primary visual reference for WPF implementation.

## UI Direction

- Work-focused retail POS interface.
- Fast scanning and checkout.
- Clear totals and payment state.
- Dense but readable operational screens.
- Strong status visibility for offline and sync states.

## Main Screens

- Login.
- POS Main.
- Product grid.
- Cart panel.
- Payment dialog.
- Receipt preview.
- Checkout recovery.
- Customer display.
- Dashboard.
- Sync status.

## UX Rules

- A cashier should understand what action is available next.
- Totals, payment state, and sync state must be visible and unambiguous.
- User-facing errors must be safe and non-technical.
- Long-running API or sync work must not freeze the UI.
- Manual refresh can remain available even when automatic refresh exists.

## WPF Implementation Notes

- Prefer bindings and commands over code-behind.
- Keep code-behind for view wiring only.
- Use ViewModels for state and command behavior.
- Use messenger messages for cross-screen refresh when direct ownership would create coupling.
- Do not dispose reusable ViewModels from `Unloaded`.

## Customer Display

The MVP may use a separate WPF window on the same monitor. Real secondary-monitor placement and display selection are Phase 2.
