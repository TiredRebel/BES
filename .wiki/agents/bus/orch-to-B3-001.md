---
title: "orch → B3: integration-test task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]", "[[index]]"]
from: orch
to: B3
seq: 1
---

# B3 Integration tests: task

You are node **B3 (Integration tests, sonnet)** in `.wiki/plan/graph.yaml`. You prove against a **real PostgreSQL**
(Testcontainers) that the database itself rejects BR1, BR2, BR3, BR4 and BR5 violations, that the migration applies
to an empty database, that the seed loads, and that the three use cases work end to end. You run in your own worktree,
in parallel with B1 (DbContext, configurations, migration with the trigger) and B2 (`TaskService`). Neither exists in
your worktree: code against the spec's contracts. The tests first run at fan-in B.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## First

1. Load the `ponytail:ponytail` skill with the Skill tool (human instruction for development nodes): the smallest
   tests that prove each spec row. The spec's test list is required.
2. Read `AGENTS.md`, then `.wiki/domain/spec.md` §2 (the analyzer list, including CA1001 and the xUnit analyzers),
   **§2a**, §3, §10, §11 (every name, the trigger), §12 (the seed rows and ids), §13 (the DbContext), §14 (the service)
   and **§15 "B3 integration tests" (your exact test list, fixture, raw SQL, expected SqlStates and constraint names)**,
   then `docs/adr/0009`.

## Files you create (only these)

- `tests/TaskManagement.IntegrationTests/PostgresFixture.cs`: the fixture and
  `[CollectionDefinition("Postgres")] public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>`.
  One container per test run, `new PostgreSqlBuilder("postgres:17-alpine").Build()`; each test gets its **own fresh
  database** (a unique `Database` name via `NpgsqlConnectionStringBuilder`, then `Database.MigrateAsync()`). A fixed
  `TimeProvider` subclass (overriding `GetUtcNow()`, returning `2026-09-18T12:00:00Z`) may live here.
- `tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs`
- `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs`
- `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs`
- `.wiki/agents/bus/B3-to-orch-001.md`: your report.

Namespace `TaskManagement.IntegrationTests` (file-scoped). Implement **every** §15 B3 test, with exactly those names,
including the grill additions: `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`,
`Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`, `Update_StatusOfCompletedTask_RejectedByBR4Trigger`,
`ListTasksByAssigneeAsync_BobFilteredByStatus_ReturnsOnlyMatchingTasks`,
`ListTasksByAssigneeAsync_UndefinedStatus_ThrowsArgumentOutOfRangeException`,
`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` and
`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`. The schema test also checks that the
trigger `trg_tasks_br3_br4` exists. Every test method has `[Trait("Category", "Integration")]` and an XML
`<summary>` naming the BR or use case it proves. Test classes are `public sealed`, carry `[Collection("Postgres")]` and
have XML docs. Raw SQL uses the exact statements of §15. Assert `SqlState` and `ConstraintName`, never message text.

## Checking your work

Your worktree can't compile the tests, because the DbContext and `TaskService` are missing. Compile anyway **outside
the repo**: in a temporary folder, build a copy of your test project that references a stub library holding copies of
the four domain files, plus **stubs** of `TaskManagementDbContext` (spec §13: constructor, `Employees`, `Tasks`) and
`TaskService` (spec §14 signatures, bodies `throw new NotImplementedException()`), with the same packages as the real
projects and copies of `Directory.Build.props` and `.editorconfig`. `dotnet build -warnaserror` must exit 0. Report it.
Never commit the stubs, and do not run the tests against them.

## Do not touch

Everything else: every `.csproj`, `src/**`, `tests/TaskManagement.UnitTests/**`, and all docs outside your report.
No new packages. Do not use the EF in-memory provider.

## Acceptance

After your commit, `git diff --stat HEAD~1` touches only your 4 test files and your report, and the out-of-repo stub
build exits 0. The tests run for real at fan-in B. Commit on your worktree branch:
`test(integration): Testcontainers tests for schema, constraints, trigger and use cases`, with the trailer
`Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.

## Questions

Send them to orch with SendMessage (`to: "main"`) and also write them to `.wiki/agents/bus/B3-to-orch-<seq>.md`. Keep
working on the rest.

## Report (`.wiki/agents/bus/B3-to-orch-001.md`, committed with your tests)

Frontmatter like this file's (`from: B3`, `to: orch`). Then: status; a table **test → BR / use case → expected
SqlState + constraint (or exception)** covering every §15 B3 test; the fixture design in 3–5 lines; the stub build
command and output; every command you ran with its real output; `git diff --stat HEAD~1`; the worktree branch name
and commit SHA; `[uncertain]` items (for example `ExecuteSqlRawAsync` wrapping, and whether EF returns the tracked,
stale `Employee` in the race test); open questions.
