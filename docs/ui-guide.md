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

Checkout and payment data continue to bind through the existing customer-display
ViewModel and display state.

EPIC-08 adds a Desktop-owned display host that:

- discovers available Windows monitor targets;
- owns at most one customer-display window per terminal UI scope;
- places the window on the selected target in fullscreen mode;
- handles DPI and target changes without duplicating windows;
- falls back safely when the selected monitor is disconnected;
- closes and releases the display window with the terminal UI scope.

Production display policy, advanced operator configuration, and non-WPF hardware
adapters remain Phase 2.
