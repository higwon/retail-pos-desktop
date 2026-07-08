# UI Changelog

## v0.10 - Receipt Preview Binding

- Bound the receipt dialog to generated completed-order receipt data.
- Added local print simulation status for MVP receipt demo use.
- Successful checkout now opens the generated receipt preview after payment completion.

## v0.9 - Checkout Recovery Binding

- Bound checkout recovery to persisted `ApprovedButOrderNotCreated` records.
- Added idempotent order completion and manager-review actions to the recovery screen.
- App startup now routes to checkout recovery when approved unresolved checkouts exist.

## v0.8 - Customer Display Binding

- Bound the customer display window to the shared checkout session.
- Customer display now reflects cart lines, item count, discounts, totals, and MVP payment/completion messages.
- Closing and reopening the customer display preserves shared checkout display state.

## v0.7 - UI Shell Implementation

- Added WPF payment and receipt dialog shells.
- Added checkout recovery, operations dashboard, and system status shells.
- Added demo navigation between the register and operational screens.
- Kept all new screens UI-only without payment, recovery, reporting, or synchronization behavior.

This document tracks meaningful UI and UX changes in the Figma design and WPF implementation plan.

Figma file:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

## v0.1 - Initial Design System

- Added base color tokens.
- Added typography scale.
- Added button, input, pill, and product card references.

## v0.2 - Login Screen

- Added login screen for cashier authentication.
- Added offline login guidance.
- Added visual panel explaining offline-first POS behavior.

## v0.3 - POS Main Screen

- Added category sidebar.
- Added product search and product grid.
- Added cart panel.
- Added cart summary and checkout actions.

## v0.4 - Payment and Recovery

- Added payment dialog.
- Added card and cash payment actions.
- Added checkout recovery screen for approved payment recovery scenarios.

## v0.5 - Customer Display

- Added customer-facing display screen.
- Added real-time cart summary for customer view.
- Added payment waiting state.
- Added thank-you and receipt guidance copy.

## v0.6 - Receipt, Dashboard, and Status Screens

- Added `06 Receipt View` for receipt preview shell implementation.
- Added `07 Dashboard Screen` for management dashboard shell implementation.
- Added `08 Status Screen` for synchronization/status shell implementation.
- Updated UI implementation docs so POS-107, POS-109, and POS-110 have Figma references.

## Planned UI Updates

- Add sales history screen.
- Add settings screen.
- Improve POS Main screen density and keyboard-first workflow.
- Add WPF-specific style mapping notes after first implementation.
- Extract repeated UI patterns into a formal design system after EPIC-02 shell screens are complete.
