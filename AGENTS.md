# AGENTS.md

This file contains common rules for AI coding agents. It is intended to be reusable across repositories.

For this repository's architecture, workflow, and implementation rules, read [docs/agent-guide.md](docs/agent-guide.md).

## Common Agent Rules

- Work from the user's actual local checkout. Verify the current branch and `git status` before making changes.
- Do not revert or overwrite user changes unless the user explicitly asks for that.
- Read the existing code and docs before changing behavior. Prefer established local patterns over new abstractions.
- Keep changes scoped to the user's request. Avoid unrelated refactors and metadata churn.
- Prefer small, reviewable PRs with clear titles, useful descriptions, and explicit validation notes.
- Stage only files that belong to the current task.
- Use non-destructive git commands by default. Do not use `git reset --hard`, broad checkout commands, or recursive deletes unless explicitly requested and approved.
- Use structured parsers or framework APIs instead of ad hoc string manipulation when practical.
- Add tests when behavior changes or when the risk is not trivial.
- If tests cannot be run, explain why and name the remaining risk.
- When reviewing code, lead with bugs, risks, regressions, or missing tests. Keep summaries secondary.
- Keep documentation and code aligned. If a decision changes, update the source-of-truth document or create a follow-up issue.
- Use clear, user-safe wording in UI-facing messages. Do not expose internal exception details to end users.
- Respond in the user's preferred language when it is clear from the conversation.

## Common PR Checklist

- Current branch is correct.
- Diff contains only intended files.
- Relevant tests or checks were run.
- Documentation was updated when behavior, workflow, architecture, or contracts changed.
- PR body explains what changed, why it changed, user/developer impact, validation, and related issue.

## Project Discovery

1. Read `README.md`.
2. Read this `AGENTS.md`.
3. Read the repository-specific agent guide if present.
4. Read only task-relevant source-of-truth docs.
5. Inspect existing code before adding abstractions.
6. Do not read every document in the repository unless necessary.
