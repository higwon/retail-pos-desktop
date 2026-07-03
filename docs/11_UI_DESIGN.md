# UI Design

## Figma Reference

Figma file:

https://www.figma.com/design/G71mpke3GSKytIXRqsjD8D/Retail-POS-UI

The Figma file is the primary UI reference for WPF implementation.

Codex and any coding agent should use this file when implementing screens, layouts, spacing, colors, typography, and reusable UI components.

## Current Figma Screens

- `00 Design System`
- `01 Login Screen`
- `02 POS Main Screen`
- `03 Payment Dialog`
- `04 Checkout Recovery`
- `05 Customer Display`
- `06 Receipt View`
- `07 Dashboard Screen`
- `08 Status Screen`

## Implementation Rules

- Do not invent a different visual direction unless the documentation changes.
- Implement screens incrementally according to `docs/10_TASK_BACKLOG.md` and `docs/13_EPICS_AND_TASKS.md`.
- Prefer reusable WPF styles and controls instead of duplicating XAML.
- Keep layout implementation MVVM-friendly.
- Do not put business logic in Views or code-behind.
- Use the Figma design as a visual target, not as generated code that must be copied directly.

## WPF Mapping Guide

Recommended mapping:

```text
Figma Frame              -> WPF View / Window
Figma Component          -> UserControl or Style
Design System Colors     -> ResourceDictionary Brushes
Typography Tokens        -> TextBlock Styles
Buttons                  -> Button Styles
Input Fields             -> TextBox / PasswordBox Styles
POS Main                 -> Main POS View
Customer Display         -> Separate WPF Window in MVP
Receipt View             -> Receipt preview View or Dialog
Dashboard Screen         -> Dashboard View
Status Screen            -> Status / Sync Status View
```

## Screen Ownership

### Login Screen

WPF target:

```text
RetailPOS.Desktop/Views/LoginView.xaml
RetailPOS.Desktop/ViewModels/LoginViewModel.cs
```

### POS Main Screen

WPF target:

```text
RetailPOS.Desktop/Views/PosMainView.xaml
RetailPOS.Desktop/ViewModels/PosMainViewModel.cs
```

### Payment Dialog

WPF target:

```text
RetailPOS.Desktop/Views/PaymentDialog.xaml
RetailPOS.Desktop/ViewModels/PaymentDialogViewModel.cs
```

### Checkout Recovery

WPF target:

```text
RetailPOS.Desktop/Views/CheckoutRecoveryView.xaml
RetailPOS.Desktop/ViewModels/CheckoutRecoveryViewModel.cs
```

### Customer Display

WPF target:

```text
RetailPOS.Desktop/Views/CustomerDisplayWindow.xaml
RetailPOS.Desktop/ViewModels/CustomerDisplayViewModel.cs
```

MVP behavior:

- Use a separate WPF window.
- It may run on the same monitor during development.
- It should update when the cart changes.
- Real secondary monitor placement is Product Phase 2.

### Receipt View

WPF target:

```text
RetailPOS.Desktop/Views/ReceiptView.xaml
RetailPOS.Desktop/ViewModels/ReceiptViewModel.cs
```

MVP shell behavior:

- Show receipt preview layout based on Figma `06 Receipt View`.
- Include placeholder receipt content only during UI Shell phase.
- Real receipt generation and device integration are later tasks.

### Dashboard Screen

WPF target:

```text
RetailPOS.Desktop/Views/DashboardView.xaml
RetailPOS.Desktop/ViewModels/DashboardViewModel.cs
```

MVP shell behavior:

- Show summary cards, chart placeholder, attention list, and recent order table based on Figma `07 Dashboard Screen`.
- Use placeholder values only during UI Shell phase.
- Real reporting, order queries, and permission behavior are later tasks.

### Status Screen

WPF target:

```text
RetailPOS.Desktop/Views/StatusView.xaml
RetailPOS.Desktop/ViewModels/StatusViewModel.cs
```

MVP shell behavior:

- Show network/system status, pending queue placeholder, and selected item detail based on Figma `08 Status Screen`.
- Use placeholder values only during UI Shell phase.
- Real synchronization, retry policy, and persistence are later tasks.

## Initial Implementation Priority

Task 1 should not implement feature screens.

Task 2 / EPIC-02 should create placeholder screens aligned with this document and the Figma file.

Visual polish can be improved after the functional shell and navigation are stable.
