# AI Instructions

This document is written for Codex or any AI coding agent working on this repository.

## Required Behavior

Before writing code:

1. Read every document under `/docs`.
2. Understand the project goal, architecture, and roadmap.
3. Implement only the requested task.
4. Do not redesign the architecture unless explicitly requested.
5. If documentation and existing code conflict, follow the documentation unless the user says otherwise.

## Project Identity

This is a general retail POS desktop system.
Do not reference any specific company, brand, or employer in code, UI, README, namespaces, sample data, or comments.

## Core Engineering Rules

- Use C# and .NET 8.
- Use WPF for the desktop client.
- Use MVVM strictly.
- Do not reference View classes from ViewModels.
- Use CommunityToolkit.Mvvm for observable properties and commands.
- Use Microsoft.Extensions.DependencyInjection for dependency injection.
- Keep business logic out of XAML code-behind.
- Keep domain rules independent from WPF and database frameworks.
- Prefer readable, professional code over compact clever code.
- Avoid temporary hacks unless explicitly marked and tracked.
- Add or update tests when business logic changes.
- Update relevant documentation when architecture or behavior changes.

## Layering Rules

The intended dependency direction is:

```text
Desktop -> Application/Domain/Infrastructure
API -> Application/Domain/Infrastructure
Infrastructure -> Application/Domain
Application -> Domain
Domain -> none
```

The Domain project must not depend on:

- WPF
- Entity Framework Core
- SQLite
- SQL Server
- ASP.NET Core
- HTTP clients
- Device-specific implementations

## UI Rules

- Make the UI suitable for cashier workflows.
- Prioritize keyboard-first operation.
- Support touch-friendly controls where reasonable.
- Keep important actions visible.
- Avoid over-designed portfolio UI that would not fit real POS work.

## Commit Style

For implementation work, use a conventional commit prefix with the POS task ID:

- `feat(POS-001): create initial solution structure`
- `fix(POS-405): preserve approved checkout recovery state`
- `refactor(POS-102): extract cart panel view`
- `test(POS-202): add cart quantity tests`
- `docs(POS-001): clarify solution structure scope`

For repository-level documentation that is not tied to a POS task, a plain conventional docs commit is acceptable:

- `docs: update project documentation`

## First Implementation Task

The first coding task should only create the solution structure, project references, DI setup, a running `MainWindow`, and an empty `NavigationHost`.
Placeholder feature screens belong to the second task.
Do not implement feature screens or business logic in the first task.
