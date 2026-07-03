# Definition of Done

This document defines when a task is considered complete.

Codex and any coding agent should use this checklist before finishing a task.

## Default Definition of Done

A task is done only when:

- [ ] The requested POS task or GitHub Issue is implemented.
- [ ] The implementation stays within the task scope.
- [ ] The solution builds successfully.
- [ ] Tests pass, or tests are clearly not required for the task.
- [ ] No unrelated future task was implemented.
- [ ] No secrets or local machine-specific paths were committed.
- [ ] Documentation is updated if behavior, architecture, API, database, or UI changed.
- [ ] Figma and `docs/11_UI_DESIGN.md` were followed if the task involved UI.
- [ ] The issue can be moved to Review.

## Task Scope Rule

Do not mark a task done if it includes large unrelated work.

Bad example:

```text
POS-001 creates solution, adds Login UI, implements SQLite, and adds checkout logic.
```

Good example:

```text
POS-001 creates only the solution and project structure.
```

## Build Rule

Before completing a code task:

```text
dotnet build
```

must succeed unless the repository is still intentionally documentation-only.

If the build cannot run, the reason must be documented in the final task note.

## Test Rule

When tests exist:

```text
dotnet test
```

should pass before the task is considered done.

If the task does not require tests, state that explicitly.

## Documentation Rule

Update docs when changing:

- Architecture
- Project references
- Public contracts
- Database schema
- API shape
- Checkout behavior
- Synchronization behavior
- UI screen behavior
- Task scope

## UI Rule

For UI tasks:

- Compare implementation with the Figma reference.
- Keep reusable styles in ResourceDictionaries where practical.
- Avoid placing business logic in code-behind.
- Keep ViewModel independent from View classes.

## Review Rule

Move the task to Review when:

- Implementation is complete.
- Build/test validation has been done.
- The implementation note explains what changed.
- Any known limitation is explicitly mentioned.

## Done Rule

Move the task to Done only after review is accepted.

Review should check:

- Scope control
- Architecture consistency
- MVVM compliance
- Naming
- Error handling
- Test coverage
- Documentation updates
- UI consistency, if applicable

## Codex Final Response Template

Codex should finish each task with:

```text
Implemented: POS-XXX

Summary:
- ...

Validation:
- dotnet build: passed/failed/not run
- dotnet test: passed/failed/not required

Docs updated:
- yes/no

Notes:
- ...
```
