# Branch Strategy

This document defines the recommended branching and commit strategy for the Retail POS Desktop project.

The project can be developed by one person, but the workflow should still look like a small professional product team.

## Default Branches

### main

`main` is the stable branch.

Rules:

- Should always build.
- Should contain reviewed or accepted work.
- Should not receive large unreviewed changes directly once implementation starts.

### develop

Optional branch for active integration.

For a solo portfolio project, `develop` is optional. If the workflow becomes heavy, use:

```text
feature branch -> develop -> main
```

If keeping the workflow simple, use:

```text
feature branch -> main
```

## Feature Branch Naming

Use the task ID from `docs/13_EPICS_AND_TASKS.md`.

Format:

```text
feature/pos-001-solution-structure
feature/pos-102-pos-main-shell
feature/pos-405-pending-checkout-flow
```

For fixes:

```text
fix/pos-405-checkout-recovery-state
```

For docs:

```text
docs/pos-001-task-definition
```

## Commit Message Format

Use a conventional prefix with the POS task ID.

Examples:

```text
feat(POS-001): create initial solution structure
fix(POS-405): preserve approved checkout recovery state
refactor(POS-102): extract cart panel view
docs(POS-001): clarify solution structure scope
test(POS-202): add cart quantity tests
```

Recommended prefixes:

```text
feat      New feature
fix       Bug fix
refactor  Internal restructuring without behavior change
docs      Documentation only
test      Tests only
chore     Build/config/tooling
style     Formatting or UI-only styling
```

## Pull Request Rule

Recommended PR title:

```text
[POS-001] Create Solution Structure
```

Recommended PR body:

```text
## Summary

- ...

## Scope

- ...

## Validation

- [ ] Build succeeded
- [ ] Tests passed or not required
- [ ] Scope limited to this task
- [ ] Docs updated if needed
- [ ] Figma followed if UI changed

## Related Issue

Closes #issue-number
```

## Codex Rule

Codex should work on one GitHub Issue or one POS task at a time.

Recommended instruction:

```text
Implement GitHub Issue #1 only.
Do not implement future tasks.
Keep the change scoped and build before finishing.
```

## When to Commit

Commit when:

- The current POS task is complete.
- The solution builds.
- Tests pass or the task does not require tests.
- The task scope is not mixed with unrelated work.

Avoid committing:

- Broken builds.
- Unrelated formatting changes.
- Large mixed feature changes.
- Secrets or local machine paths.
