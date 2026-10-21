# Agent Instructions

## Project - OpenBudget

{{ADD SMALL DESCRIPTION OF PROJECT}}

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

## Testing

{{ADD TESTING RULES}}

## Working With Other Agents

Multiple coding agents may work on this repository.

Before editing:

- check the current branch and Git status
- re-read files before modifying them
- preserve unrelated changes
- do not reset, clean, revert, force-push, or discard another agent's work unless explicitly instructed

Agent-specific configuration may exist in `.github/`, `.claude/`, `.codex/`, or similar directories.

Those files may provide tool-specific behavior, but this `AGENTS.md` and the project documentation remain the shared repository-level instructions.

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