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

## POS-805 UI Smoke Checklist

Run this checklist on Windows after UI or device-simulator layout changes. Use both
100% and 150% display scaling, and repeat the secondary-display steps with monitors
that use different scaling values.

### Supported sizes and dense data

- Main window at 1440x900 and the supported minimum 1180x720: header actions remain
  reachable, status/cashier text does not overlap, and catalog/cart panels remain usable.
- Payment at 520x600 and Receipt at 700x700 minimum: status, long approval codes,
  transaction references, line totals, and action buttons remain readable or expose the
  complete value through a tooltip.
- Device Simulator at 980x780 and the supported minimum 860x700: every tab can scroll
  its history, long request identifiers do not cover response controls, and ComboBox
  selections remain visible.
- Dashboard and Status at minimum size: summary cards, recent order rows, device badges,
  sync queue details, and refresh actions remain reachable without horizontal clipping.
- Load the 5,000-product reference dataset: the cashier grid initially presents 50 cards,
  `Load more products` adds the next 50, and category/search changes reset the page.
- Assign an unknown product category: the neutral generic product image is shown instead
  of an unrelated known-category image.

### Keyboard and assistive behavior

- Starting at the first input, use only Tab and Shift+Tab through Login, POS, Payment,
  Receipt, Status, and every Simulator tab; focus order follows the visual workflow.
- Every focused button and text input has a visible blue focus outline at both scaling
  values. Enter/Space activates product cards and buttons exactly once.
- Payment and Receipt keep Tab navigation inside the modeless workflow window until it
  is closed; Close/Done remain keyboard reachable.
- Screen-reader names identify product results, sync queue, payment actions, receipt
  actions, simulator tabs, scanner filters, and printer responses.
- Dynamic errors are announced assertively; successful or informational status changes
  are announced politely. Color is supplementary to readable state text.

### Per-monitor display

- Open Customer Display on each secondary monitor, then move it between targets while
  open. The existing window moves without duplication and fills the target working area.
- Disconnect the selected monitor and confirm the host reports unavailable/closed without
  leaving an inaccessible window. Reconnect it and confirm the target list and status refresh.
- Move Payment, Receipt, and Device Simulator between 100% and 150% monitors. Text stays
  sharp, controls keep their minimum hit size, and no metadata overlaps after the DPI change.
