# Agent Instructions

## Project - OpenBudget

OpenBudget is a local budgeting application with a React/TypeScript UI, .NET API,
and PostgreSQL persistence, orchestrated through .NET Aspire.

## Before making changes
1. Read docs/*

Always:
1. Inspect the current repository and Git status.
2. Read the documentation relevant to the task.
3. Follow existing architecture and conventions.
4. Prefer extending existing patterns over introducing new ones.
5. Implement the smallest complete change required.
6. Add or update tests.
7. Run relevant tests before finishing.
8. Update documentation when behavior or architecture changes.

Do not implement future functionality unless required by the current task.

## Documentation

Detailed project design lives in `docs/`.

Read the relevant documents before modifying anything.

Use these documents as the source of truth for established architecture and milestone decisions.

Whenever a change affects behavior, architecture, APIs, configuration, setup, workflows, data models, or implementation decisions, update the relevant files in `docs/` and update `README.md` when the change affects project overview, setup, usage, prerequisites, or developer onboarding.

A task is not complete until the code, tests, README, and relevant documentation are consistent.

Do not duplicate detailed documentation into this file.

## Coding Guidelines

Follow existing conventions in `docs/conventions.md`.

In general:

- Keep implementations simple and explicit.
- Prefer working vertical slices over speculative architecture.
- Avoid unnecessary dependencies.
- Use nullable reference types.
- Prefer async APIs for I/O.
- Propagate `CancellationToken` where appropriate.
- Keep public API contracts separate from persistence entities.
- Validate external and AI-generated input.
- Validate filesystem paths and prevent path traversal.
- Never log secrets.
- Do not bypass application services from API, MCP, or Worker code.
- MCP parity: every new or changed user-facing feature or API capability must also be exposed through the MCP server in the same change (tool + tests + a row in the UI parity table in `docs/mcp.md`). The only exception is a capability that is security-sensitive (authentication, agent-token management, privilege changes); list it there as excluded with the reason and flag it to the user.

## Testing

- Every new feature and behavior change must ship with automated tests in the same change. A feature is not complete until its tests pass.
- Add focused unit tests for domain calculations, validation, boundary conditions, and failure cases. Test observable behavior rather than implementation details.
- Add integration tests for every new or changed API/persistence workflow. Exercise the real HTTP boundary, application services, EF migrations, and an isolated PostgreSQL database; do not substitute an in-memory provider for database coverage.
- Cover successful writes and reloads, invalid input with no partial writes, stale/concurrent edits, command replay/idempotency, and transaction rollback where applicable.
- Add UI component tests for new forms, validation, loading/empty/error states, and recovery actions. Verify the complete user path through the running application for each milestone.
- Use deterministic synthetic data. Tests must never mutate the developer's budget database or depend on test order, external financial services, or secrets committed to the repository.
- Add regression tests with bug fixes. Keep existing tests passing; do not remove or weaken assertions to make a change pass.
- Run the relevant unit, integration, and UI suites before finishing, and report their results and any blocked verification explicitly. See `docs/developer-guide.md` for commands.

## Working With Other Agents

Multiple coding agents may work on this repository.

Before editing:

- check the current branch and Git status
- re-read files before modifying them
- preserve unrelated changes
- do not reset, clean, revert, force-push, or discard another agent's work unless explicitly instructed

Agent-specific configuration may exist in `.github/`, `.claude/`, `.codex/`, or similar directories.

Those files may provide tool-specific behavior, but this `AGENTS.md` and the project documentation remain the shared repository-level instructions.

### Delegating requests to worktrees and subagents

For every user request, judge its complexity and delegate when it pays off:

- **Trivial** (copy, a style tweak, a one-file fix): do it directly in the current working branch.
- **Moderate or complex** (a new feature, a cross-layer change, or anything touching the API, migrations, or several UI areas): run it in a subagent in its own Git worktree (branch `worktree-<feature>`). Pick the model and effort to match the work: a smaller model or lower effort for mechanical or styling changes, the most capable model at high effort for ledger, money, security, or schema work.
- Independent requests may run as parallel subagents, each in a separate worktree. Give each one a self-contained brief: the goal, the screenshots or acceptance criteria, the relevant files, and the testing expectations from this file.

When a subagent finishes:

1. Review its diff and confirm its tests pass.
2. Merge its branch back into the **current working branch**, not necessarily `main`. Resolve conflicts by keeping both sides' intent, then check `dotnet ef migrations has-pending-model-changes` when migrations are involved.
3. Run the relevant unit, integration, and UI suites on the merged result.
4. **Always rebuild and restart the Aspire AppHost** (`dotnet run --project src/OpenBudget.AppHost`) so the user can see the changes, and confirm that the UI and `/health` respond.
   - The running AppHost locks the API binaries, so stop it before `dotnet build` or `dotnet test` and restart it afterwards.
   - Tell the user before stopping it, and keep the downtime short.
5. Report what was merged, the test results, and any deviations or unverified visuals.

Never delete a worktree or branch that still has unmerged work.

## When Unsure

Before inventing a new pattern:

1. Search the existing implementation.
2. Check the relevant `docs/` file.
3. Follow the established approach when one exists.
4. Only introduce a new architectural pattern when the existing design cannot reasonably support the requirement.

## Git
- Never commit directly to main.
- Use conventional commits.
- One logical change per commit.

## Skills
Reusable workflows are located in `.agents/skills/`.

## Roadmap

Before implementing roadmap work, read the relevant roadmap and its acceptance
criteria. The active UI roadmap is `docs/roadmap.md`.

## Development Principles

- After updating any backend code, remember to rebuild and rerun the aspire app host.
- Always try to create reusable components whenever a possibility exists!
