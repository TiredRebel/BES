# AGENTS.md

Canonical instructions for every coding agent working in this repo (Claude Code, Codex, Cursor, Antigravity).
Tool-specific files (`CLAUDE.md`, `.cursor/rules/agents.mdc`, `.agents/rules/agents.md`) only point here.

**Start every session by reading `.wiki/index.md`.** It maps the domain spec, business rules, ADRs,
the execution graph (`.wiki/plan/graph.yaml`, with each node's status) and the session log.

## What this repo is

The data layer of **Task Management**, the internal CRM module for assigning, executing and controlling employees'
tasks: PostgreSQL + EF Core.
Covers employees, tasks, task assignment, deadlines and statuses, enforcing business rules BR1–BR5
(defined in `.wiki/domain/`). Scope is backend/data only: domain, persistence, an application service, tests.
A web API, UI or server host is out of scope.

## Hard rules

1. English everywhere: code, XML docs, READMEs, ADRs, wiki pages.
2. EF Core mapping lives in `IEntityTypeConfiguration<T>` classes (Fluent API). Entities stay free of
   data-annotation attributes; `[Key]`, `[Required]`, `[Table]`, `[Column]`, `[MaxLength]` and friends must return
   zero hits in entity files.
3. Every schema change is an EF Core migration. SQL dumps are never shipped.
4. Every package, API, CLI flag, config key and path you write is verified first against the installed SDK,
   restored packages, NuGet, or Microsoft Learn. When you cannot verify it, write `[uncertain]` and ask the human.
   Report "not found" instead of inventing a path or API.
5. A step is done when its evidence checks out: build output, test output, `git diff`.
6. Ask the human before: adding a NuGet package not in the stack table below, deleting files, changing the
   approved data model (`.wiki/domain/`), or acting on an ambiguous requirement.

## Stack (verified 2026-09-18)

| Item | Version |
|---|---|
| .NET SDK / TFM | 10.0.401 / `net10.0` (LTS) |
| Microsoft.EntityFrameworkCore, .Relational, .Design | 10.0.12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| dotnet-ef (global tool) | 10.0.12 |
| xunit / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 2.9.3 / 3.1.4 / 17.14.1 |
| Testcontainers.PostgreSql | 4.15.0 |
| PostgreSQL image for tests | `postgres:17-alpine` |

`Microsoft.EntityFrameworkCore.Relational` is referenced explicitly in Infrastructure (approved at N2): Npgsql 10.0.3
only requires Relational >= 10.0.4, and without the pin every project referencing Infrastructure fails with MSB3277.

`Directory.Build.props` applies to every project: `Nullable`, `TreatWarningsAsErrors`,
`AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` with CS1591 as an error.
A public member without XML docs fails the build. `.editorconfig` scopes two relaxations, and only these two:
CA1707 is off under `tests/` (test names are `Method_Scenario_Expected`), and `src/**/Migrations/*.cs` is marked
generated code.

## Conventions

- XML docs on every type and member: `<summary>`, `<param>`, `<returns>`, `<exception>`, and a `<remarks>` naming
  the business rule (BR1–BR5) the code enforces.
- Each Fluent configuration class documents which index or constraint it creates and why.
- Each migration starts with a header comment listing its schema changes.
- Timestamps are `DateTimeOffset` in UTC, stored as `timestamptz`.
- Tests carry `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]`. Integration tests run
  against a real PostgreSQL via Testcontainers; the EF in-memory provider is never used for constraint tests.
- One ADR per non-trivial decision: `docs/adr/NNNN-title.md` (context, decision, consequences).

## Commands (run from the repo root)

```bash
dotnet build -warnaserror
dotnet test --filter Category=Unit
dotnet test                                   # all tests; integration tests need Docker running
dotnet ef migrations add <Name> --project src/TaskManagement.Infrastructure
dotnet ef migrations list --project src/TaskManagement.Infrastructure --no-connect
dotnet ef database update --project src/TaskManagement.Infrastructure --connection "<connection string>"
codegraph sync .                              # refresh the code graph index after code changes
```

Solution layout and the exact file list per node: `.wiki/domain/spec.md` §1.

## Code graph

This repo's code graph tool is **CodeGraph** (`@colbymchenry/codegraph` 1.6.0, CLI `codegraph`). It replaces any
user-level graphify instructions here. The index lives in `.codegraph/` (machine-local, gitignored by its own
`.gitignore`); rebuild it with `codegraph init -y` on a fresh clone. The MCP server (`codegraph serve --mcp`) is wired
project-scoped in `.mcp.json` (Claude Code), `.cursor/mcp.json` (Cursor) and `.codex/config.toml` (Codex, trusted
projects only). For symbol questions, use `codegraph query <name>`, `codegraph callers <symbol>`,
`codegraph impact <symbol>` or the `codegraph_explore` MCP tool. After each fan-in, the code map summary in
`.wiki/codemap.md` is regenerated from CodeGraph.

## Workflow

- Conventional Commits. Push nowhere.
- Every commit appends an entry to `.wiki/log.md` (date, agent, what changed, commit SHA).
- A commit that changes an entity or a business rule updates that entity's or rule's page in `.wiki/domain/`
  in the same commit.
- Agents talk through the message bus: one file per message in `.wiki/agents/bus/`, named
  `<from>-to-<to>-<seq>.md` (for example `A2-to-A1-001.md`). The orchestrator's id is `orch`.
- Worker reports carry: status, files changed, commands run with their real output, open questions.
- Development workers load the `ponytail:ponytail` skill before writing code: the smallest code that meets the spec.
  What the brief or spec requires (XML docs, BR guards, constraints, tests) is required, not optional.
