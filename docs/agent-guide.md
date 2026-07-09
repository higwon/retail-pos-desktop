# Retail POS Agent Guide

This document contains repository-specific guidance for AI coding agents.

Common agent behavior rules are in [../AGENTS.md](../AGENTS.md).

## Task Document Map

Read the documents relevant to the current task. Do not read every document by default.

General orientation:

- [../README.md](../README.md)
- [../AGENTS.md](../AGENTS.md)
- [Agent Guide](agent-guide.md)

Architecture or layering:

- [Architecture](architecture.md)
- [Decisions](decisions.md)

API work:

- [API Contracts](api-contracts.md)
- [Architecture](architecture.md)

Sync or offline work:

- [Sync and Offline](sync-and-offline.md)
- [API Contracts](api-contracts.md)
- [Decisions](decisions.md)

UI work:

- [UI Guide](ui-guide.md)
- [Architecture](architecture.md)

Issue planning:

- [Epics and Tasks](epics-and-tasks.md)
- [Roadmap](roadmap.md)

Development workflow:

- [Development Workflow](development-workflow.md)
- [Epics and Tasks](epics-and-tasks.md)

## Repo-Specific Rules

- Keep Domain independent from all other projects.
- Keep Application independent from Infrastructure, Desktop, and Api.
- Register DI in executable composition roots or targeted DI extension methods called by those roots.
- Do not introduce a shared contracts project unless duplication becomes a real maintenance problem and the docs are updated first.
- Money uses `decimal`.
- Currency is KRW.
- Persist timestamps in UTC and display them in local time.
- Retry policy is 1, 2, 4, 8, and 16 minutes, with automatic retry stopped after 5 attempts.
- Refund and cancellation are outside MVP.

## ViewModel Rules

- Use CommunityToolkit.Mvvm patterns.
- `ObservableObject` is the default base.
- Commands should use toolkit command types.
- ViewModels that subscribe to events, messenger messages, timers, or long-lived services implement `IDisposable`.
- Unregister messenger recipients in `Dispose`.
- Do not call ViewModel `Dispose` from WPF `Unloaded`.
- Dialog/window-owned ViewModels may be disposed from close events when the view lifetime truly ends.

## Sync Rules

- `PendingCheckout` is persisted before payment approval.
- Order save and sync queue creation happen before pending checkout completion.
- Server stock is authoritative.
- Local stock is an estimated display value that accounts for pending local deductions.
- Order upload idempotency identity is `storeId + terminalId + localOrderId`.
- `idempotencyKey` must remain stable across retries.
- Idempotency conflicts are non-retryable and should move toward manual review.

## Testing Expectations

- Behavior changes need focused tests.
- Shared logic or sync changes usually need broader tests.
- For docs-only changes, run markdown/diff checks when practical; code tests are optional unless code changed.
- Before PR, at least confirm the diff and working tree status.

## PR Expectations

Use draft PRs by default.

The PR body should include:

- Summary.
- What changed.
- Why it changed.
- Validation.
- Related issue or note if there is no issue.

Keep PR descriptions concrete. The user values enough detail to review without re-reading every changed file first.
