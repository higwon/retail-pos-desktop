# ADR-001: Use WPF for the Windows POS Client

## Status

Accepted

## Context

The project targets a Windows desktop POS environment.

The application should demonstrate:

- Native Windows desktop application development
- MVVM architecture
- Hardware/peripheral integration concepts
- Local database usage
- Offline-first operation
- Long-running stable desktop UI behavior

## Decision

Use WPF for the desktop client.

## Reasoning

WPF is a strong fit because:

- It is native to Windows desktop development.
- It supports MVVM well.
- It is suitable for complex business UI.
- It can work with Windows APIs and device integration patterns.
- It matches the target job requirements for WinForms/WPF and C#/.NET desktop development.

## Consequences

Positive:

- Strong alignment with Windows POS application requirements.
- Good separation between UI and business logic through MVVM.
- Suitable for multiple windows such as cashier screen and customer display.

Trade-offs:

- Windows-only client.
- UI implementation requires careful ResourceDictionary and style management.
- Modern UI polish requires additional design effort.
