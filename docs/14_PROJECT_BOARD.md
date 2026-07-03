# Project Board Workflow

This project should be managed like a small product team project.

The preferred board style is Kanban.

## Recommended Columns

```text
Backlog -> Todo -> In Progress -> Review -> Done
```

Optional later column:

```text
Release
```

## Column Meaning

### Backlog

Ideas or planned tasks that are not ready for implementation yet.

### Todo

Tasks that are clearly specified and ready for Codex implementation.

### In Progress

Tasks currently being implemented.

Only one or two tasks should be in progress at the same time.

### Review

Implementation is complete and needs architectural, UI, or code review.

### Done

Task is merged or accepted.

## Task ID Rule

Every implementation task should use a task ID from `docs/13_EPICS_AND_TASKS.md`.

Examples:

```text
POS-001 Create Solution Structure
POS-102 POS Main Shell
POS-405 PendingCheckout Flow
```

## GitHub Issue Rule

Each meaningful implementation task should have a GitHub Issue.

Issue title format:

```text
[POS-001] Create Solution Structure
```

Issue body should include:

- Goal
- Scope
- Acceptance criteria
- Out of scope
- Related docs
- Codex prompt

## Branch Rule

Recommended branch naming:

```text
feature/pos-001-solution-structure
feature/pos-102-pos-main-shell
fix/pos-405-pending-checkout-recovery
```

## Commit Rule

Recommended commit messages:

```text
feat(POS-001): create initial solution structure
fix(POS-405): preserve approved checkout recovery state
refactor(POS-102): extract cart panel view
```

## Pull Request Rule

Recommended PR title:

```text
[POS-001] Create Solution Structure
```

Recommended PR checklist:

```text
- [ ] Builds successfully
- [ ] Tests pass or are not required for this task
- [ ] Scope limited to the issue
- [ ] Docs updated if needed
- [ ] Figma followed if UI changed
```

## Codex Working Rule

Codex should implement one issue at a time.

Recommended prompt:

```text
Read README.md and every document under /docs first.
Follow docs/00_AI_INSTRUCTIONS.md strictly.
Implement GitHub Issue #[issue number] only.
Do not implement future tasks.
Build and test before finishing.
```

## Manual GitHub Project Setup

The current connector can create issues and files, but may not create a GitHub Projects board directly.

If needed, create a GitHub Project manually in the GitHub UI:

1. Open the repository.
2. Go to Projects.
3. Create a new project.
4. Choose Board view.
5. Add columns: Backlog, Todo, In Progress, Review, Done.
6. Add issues created from the POS task list.

The board is optional for coding, but useful for portfolio presentation and progress tracking.
