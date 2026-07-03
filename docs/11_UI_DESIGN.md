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

## Implementation Rules

- Do not invent a different visual direction unless the documentation changes.
- Implement screens incrementally according to `docs/10_TASK_BACKLOG.md`.
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

## Initial Implementation Priority

Task 1 should not implement feature screens.

Task 2 should create placeholder screens aligned with this document and the Figma file.

Visual polish can be improved after the functional shell and navigation are stable.
