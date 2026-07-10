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

## Device Simulator

The Desktop-only Device Simulator is a modeless window available when
`DeviceSimulation:Enabled` is true. It uses one tab per simulated device and remains
separate from cashier-facing views and ViewModels.

POS-702 introduces the shared window and Receipt Printer tab with connection state,
operational state, next outcome, response delay, connect/disconnect, apply, and reset
controls. POS-701, POS-703, and POS-704 add Barcode Scanner, Card Terminal, and Customer
Display tabs to the same window.

Simulator tabs use dedicated control ViewModels. Code-behind is limited to window
lifecycle wiring, and simulator controls are never injected into cashier ViewModels.

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
