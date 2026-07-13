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
- Scan-first Register with a dominant current-sale table.
- Full-screen Product Search with category filtering.
- Transaction totals and action panel.
- Payment dialog.
- Receipt preview.
- Checkout recovery.
- Customer display.
- Dashboard.
- Sync status.

## In-Window Cashier Workflow

EPIC-10 moves cashier work into typed MainWindow screen transitions. The target screen
map is:

```text
Login -> Register -> Product Search -> Register
                  -> Card Payment -> Receipt Detail
                  -> Cash Payment -> Receipt Detail
                  -> Receipt History -> Receipt Detail
Register <-> Recovery
Register <-> Dashboard / Status
```

Push transitions retain a valid return screen, replace transitions remove intermediate
completion states such as payment, and reset transitions are reserved for Login,
Register, Receipt History, Recovery, Dashboard, and Status. Duplicate navigation is a
no-op and invalid transitions fail without changing the current screen.

Every active screen must be present in NavigationHost's screen-to-view map. The map is
registered before initial rendering, and the navigator rejects an unregistered
destination before changing state. Add a new View, its map entry, and its first
transition path in the same PR.

Workflow navigation does not replace authentication. Login/session state controls which
root actions are exposed; root reset only clears navigation history.

POS-901 establishes the navigator and migrates existing Login, Register, Recovery,
Dashboard, and Status transitions. POS-902 makes Register hardware-scanner-first and
adds Product Search as a full in-window screen. Manual barcode lookup belongs in Product
Search rather than a duplicate Register input. Signed-in root navigation uses a fixed right-side
task rail with the active screen highlighted; Product Search is available from that rail.
Selecting a Product Search row updates the fixed detail panel only. The cashier reviews
the image and product metadata, chooses a quantity from 1 to 99, and uses `Add to cart`
to update the sale and return to Register.

Payment and Receipt continue using their current
workflow windows until their in-window replacements are implemented. Device Simulator
and Customer Display keep their separate-window responsibilities.

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
  reachable, status/cashier text does not overlap, and the sale table, scanner feedback,
  totals, and transaction actions remain usable.
- Payment at 520x600 and Receipt at 700x700 minimum: status, long approval codes,
  transaction references, line totals, and action buttons remain readable or expose the
  complete value through a tooltip.
- Device Simulator at 980x780 and the supported minimum 860x700: every tab can scroll
  its history, long request identifiers do not cover response controls, and ComboBox
  selections remain visible.
- Dashboard and Status at minimum size: summary cards, recent order rows, device badges,
  sync queue details, and refresh actions remain reachable without horizontal clipping.
- Load the 5,000-product reference dataset: Product Search initially presents 50 table rows,
  `Load more` adds the next 50, category/search changes reset the page, and row selection
  updates the detail panel without changing the current sale.

### Keyboard and assistive behavior

- Starting at the first input, use only Tab and Shift+Tab through Login, POS, Payment,
  Receipt, Status, and every Simulator tab; focus order follows the visual workflow.
- Every focused button and text input has a visible blue focus outline at both scaling
  values. Enter/Space activates product selection and action buttons exactly once.
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
