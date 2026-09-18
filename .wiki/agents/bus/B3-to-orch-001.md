---
title: "B3 → orch: integration-test report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]", "[[index]]"]
from: B3
to: orch
seq: 1
---

# B3 integration tests: report

## Status

Done. All four files from the task's file list were written, every spec §15 B3 test (33 total, including all five
grill-added names) is present with the exact required name, and a stub build outside the repo compiles them with
`-warnaserror` exit 0. Tests are committed on the worktree branch. They cannot run for real in this worktree: B1's
`TaskManagementDbContext`/migration and B2's `TaskService` do not exist here (by design — B1/B2/B3 run in parallel).
They run for real at fan-in B.

## Test → BR / use case → expected SqlState + constraint (or exception)

`MigrationAndSeedTests` (4 tests, no BR — schema/seed shape):

| Test | Checks | Expected |
|---|---|---|
| `Migrate_EmptyDatabase_AppliesAllMigrationsAndLeavesNonePending` | migration applies to empty DB | `GetPendingMigrationsAsync()` empty, `GetAppliedMigrationsAsync()` == `GetMigrations()`, non-empty |
| `Schema_AfterMigration_HasSpecifiedConstraintsAndIndexes` | every §11 name + trigger exists | all 8 constraint names, 4 index names present; `pg_trigger` on `tasks` == exactly `["trg_tasks_br3_br4"]` |
| `Seed_Employees_MatchSpec` | §12 employees | 3 rows, every column (Alice/Bob active, Carol inactive) |
| `Seed_Tasks_MatchSpec` | §12 tasks | 3 rows, every column (BR1/BR2/BR5 shape) |

`DatabaseConstraintTests` (13 tests):

| Test | BR / mechanism | Expected SqlState / constraint (or exception) |
|---|---|---|
| `Insert_CompletedWithoutCompletedAt_RejectedByBR1Check` | BR1 | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Insert_NewWithCompletedAt_RejectedByBR1Check` | BR1 | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check` | BR1 | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check` | BR2 | `23514` / `ck_tasks_br2_due_at_not_before_planned_start_at` |
| `Insert_DueAtEqualsPlannedStartAt_Accepted` | BR2 boundary | no exception, 1 row affected |
| `Insert_AssigneeEqualsCreator_RejectedByBR5Check` | BR5 | `23514` / `ck_tasks_br5_assignee_not_creator` |
| `Insert_UnknownStatus_RejectedByStatusCheck` | enum CHECK | `23514` / `ck_tasks_status_valid` |
| `Insert_DuplicateEmail_RejectedByUniqueIndex` | unique index | `23505` / `ux_employees_email` |
| `Delete_EmployeeWithTasks_RejectedByRestrictForeignKey` | restrict FK | `23503` / `fk_tasks_employees_creator_id` |
| `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` | BR4 via `xmin` | `DbUpdateConcurrencyException` on the second context's `SaveChangesAsync` |
| `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger` | BR3 (trigger) | `23514` / `trg_tasks_br3_assignee_active` |
| `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger` | BR3 doesn't mask FK | `23503` / `fk_tasks_employees_assignee_id` |
| `Update_StatusOfCompletedTask_RejectedByBR4Trigger` | BR4 (trigger) | `23514` / `trg_tasks_br4_final_status` |

`TaskServiceTests` (16 tests):

| Test | BR / use case | Expected |
|---|---|---|
| `CreateTaskAsync_ValidInput_PersistsNewTask` | use case 1 | persisted row matches input, re-read through a second context |
| `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing` | BR3 (service) | `BusinessRuleViolationException` `RuleId == "BR3"`; no row persisted |
| `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` | BR3 (service, before domain BR5) | `RuleId == "BR3"` |
| `CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` | BR5 | `RuleId == "BR5"` |
| `CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` | BR2 | `RuleId == "BR2"` |
| `CreateTaskAsync_UnknownCreator_ThrowsKeyNotFoundException` | not found | `KeyNotFoundException` |
| `CreateTaskAsync_UnknownAssignee_ThrowsKeyNotFoundException` | not found | `KeyNotFoundException` |
| `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` | use case 2, BR1 | `Status == Completed`, `CompletedAt == 2026-09-18T12:00:00Z`, persisted |
| `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged` | BR4 | `RuleId == "BR4"`; row unchanged |
| `ChangeTaskStatusAsync_UnknownTask_ThrowsKeyNotFoundException` | not found | `KeyNotFoundException` |
| `ListTasksByAssigneeAsync_Bob_ReturnsHisTasksOrderedByDueAt` | use case 3 | `[…0001, …0003]`, null `DueAt` last |
| `ListTasksByAssigneeAsync_EmployeeWithoutTasks_ReturnsEmpty` | use case 3 | empty list |
| `ListTasksByAssigneeAsync_UnknownEmployee_ReturnsEmpty` | use case 3 | empty list |
| `ListTasksByAssigneeAsync_BobFilteredByStatus_ReturnsOnlyMatchingTasks` | use case 3 + filter | Theory: `(New, …0001)`, `(Cancelled, …0003)`; exactly 1 task each |
| `ListTasksByAssigneeAsync_UndefinedStatus_ThrowsArgumentOutOfRangeException` | input validation | `ArgumentOutOfRangeException` |
| `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` | BR3 race, DB + translation | `BusinessRuleViolationException` `RuleId == "BR3"`; `InnerException` is `DbUpdateException` whose `InnerException` is `PostgresException` with `ConstraintName == "trg_tasks_br3_assignee_active"`; no row persisted |

Total: 4 + 13 + 16 = **33 tests**, matching every name listed in the task brief (including all five grill additions:
`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`,
`Update_StatusOfCompletedTask_RejectedByBR4Trigger`, `ListTasksByAssigneeAsync_BobFilteredByStatus_ReturnsOnlyMatchingTasks`,
`ListTasksByAssigneeAsync_UndefinedStatus_ThrowsArgumentOutOfRangeException`,
`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`,
`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`).

## Fixture design

`PostgresFixture : IAsyncLifetime` starts one `postgres:17-alpine` container in `InitializeAsync` and disposes it in
`DisposeAsync`; `[CollectionDefinition("Postgres")] PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>`
shares it across the whole collection. `CreateDatabaseAsync()` copies `GetConnectionString()` into an
`NpgsqlConnectionStringBuilder` with a unique `Database = "tdb_" + Guid.NewGuid():N`, then calls a static
`CreateContext(connectionString)` and `Database.MigrateAsync()` on it, returning the connection string. Every test
class implements `IAsyncLifetime` itself: `InitializeAsync` calls `CreateDatabaseAsync()` once (a fresh, pristine,
already-migrated-and-seeded database per test method — xUnit creates a new test-class instance per test), and tests
needing a second, independent context (concurrency, "re-read through a second context") call the static
`PostgresFixture.CreateContext(connectionString)` again on the same connection string. `FixedTimeProvider : TimeProvider`
overrides `GetUtcNow()` to return `2026-09-18T12:00:00Z` and lives in the same file, as the spec allows.

## Stub build

Per the task, the worktree cannot compile these tests (B1's `TaskManagementDbContext`/migration and B2's
`TaskService` don't exist here). Compiled a standalone copy **outside the repo**, in the session scratchpad
(`…/scratchpad/B3Stub`), never committed:

```
B3Stub/
  Directory.Build.props        (copy of repo root)
  .editorconfig                 (copy of repo root)
  src/StubLib/StubLib.csproj    (EF Core 10.0.12, Relational 10.0.12, Design 10.0.12, Npgsql.EFCore.PostgreSQL 10.0.3)
    Employee.cs, TaskItem.cs, TaskItemStatus.cs, BusinessRuleViolationException.cs   (verbatim copies of A1's files)
    TaskManagementDbContext.cs  (stub: ctor(DbContextOptions<TaskManagementDbContext>), Employees, Tasks — spec §13)
    TaskService.cs              (stub: exact §14 signatures, bodies `throw new NotImplementedException()`)
  tests/TaskManagement.IntegrationTests/TaskManagement.IntegrationTests.csproj
    (same 4 packages as the real csproj: Microsoft.NET.Test.Sdk 17.14.1, Testcontainers.PostgreSql 4.15.0,
     xunit 2.9.3, xunit.runner.visualstudio 3.1.4; references StubLib instead of src/*)
    PostgresFixture.cs, MigrationAndSeedTests.cs, DatabaseConstraintTests.cs, TaskServiceTests.cs
    (verbatim copies of the committed files)
```

The `tests/` folder name was kept identical to the real repo path so the repo's own `.editorconfig` glob
`[tests/**.cs]` (which turns off CA1707 for `Method_Scenario_Expected` names) applies the same way in the stub.

Command and output (clean rebuild, from `B3Stub/tests/TaskManagement.IntegrationTests`):

```
$ rm -rf ../../src/StubLib/bin ../../src/StubLib/obj bin obj
$ dotnet build -warnaserror
  Determining projects to restore...
  Restored …/B3Stub/tests/TaskManagement.IntegrationTests/TaskManagement.IntegrationTests.csproj (in 256 ms).
  Restored …/B3Stub/src/StubLib/StubLib.csproj (in 259 ms).
  StubLib -> …/B3Stub/src/StubLib/bin/Debug/net10.0/StubLib.dll
  TaskManagement.IntegrationTests -> …/B3Stub/tests/TaskManagement.IntegrationTests/bin/Debug/net10.0/TaskManagement.IntegrationTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.06
```

`dotnet list package` (in the stub test project) confirms exact resolved versions, no floating bumps:

```
   Top-level Package                Requested   Resolved
   > Microsoft.NET.Test.Sdk         17.14.1     17.14.1
   > Testcontainers.PostgreSql      4.15.0      4.15.0
   > xunit                          2.9.3       2.9.3
   > xunit.runner.visualstudio      3.1.4       3.1.4
```

Two fixups were needed before it built clean, both compiler/analyzer errors, not guesses:
1. `PostgresFixture.CreateContext` didn't touch instance state → CA1822 ("can be marked static") fired as an error.
   Made it `static`.
2. Every call site then had to change from `_fixture.CreateContext(...)` to `PostgresFixture.CreateContext(...)`
   (CS0176: a static member can't be reached through an instance reference).

No CS1591 (missing XML doc) and, notably, **no CA1001** fired on any of the three test classes or on `PostgresFixture`
itself, even though each holds a disposable `TaskManagementDbContext`/`PostgreSqlContainer` field and only
implements `IAsyncLifetime` (not `IDisposable`/`IAsyncDisposable`). This resolves the spec's `[uncertain]` on that
point empirically: under EF Core 10.0.12 / SDK 10.0.401's `analysislevel_10_recommended.globalconfig`, CA1001 does
not require `IAsyncDisposable`-only ownership to be exposed.

## Commands run, with real output

Package/API verification (before writing code, all from `~/.nuget/packages`):

```
$ ls ~/.nuget/packages/testcontainers.postgresql/ ~/.nuget/packages/npgsql/ ~/.nuget/packages/microsoft.entityframeworkcore.relational/
testcontainers.postgresql: 4.15.0
npgsql: 10.0.3
microsoft.entityframeworkcore.relational: 10.0.12, 10.0.4, 9.0.19
```

PowerShell reflection against the restored `Npgsql.dll` (10.0.3):

```
$asm = [System.Reflection.Assembly]::LoadFrom(".../npgsql/10.0.3/lib/net10.0/Npgsql.dll")
$asm.GetType("Npgsql.PostgresErrorCodes").GetFields() | where Name -match "CheckViolation|UniqueViolation|ForeignKeyViolation"
→ ForeignKeyViolation = 23503
  UniqueViolation = 23505
  CheckViolation = 23514
$asm.GetType("Npgsql.PostgresException").GetProperties() | where Name -match "SqlState|ConstraintName"
→ string SqlState
  string ConstraintName
```

PowerShell reflection against `Microsoft.EntityFrameworkCore.Relational.dll` (10.0.12), confirming every migration
and raw-SQL API the tests use:

```
GetMigrations(DatabaseFacade) -> IEnumerable<string>
GetAppliedMigrationsAsync(DatabaseFacade, CancellationToken) -> Task<IEnumerable<string>>
GetPendingMigrationsAsync(DatabaseFacade, CancellationToken) -> Task<IEnumerable<string>>
MigrateAsync(DatabaseFacade, CancellationToken) -> Task
ExecuteSqlRawAsync(DatabaseFacade, string, ...) -> Task<int>
SqlQueryRaw(DatabaseFacade, string, object[]) -> IQueryable<TResult>
```

`grep`/reflection against `xunit.core.dll` (2.9.3), confirming `IAsyncLifetime.InitializeAsync()`/`DisposeAsync()`
both return `Task` (no parameters), `ICollectionFixture<T>`, `CollectionDefinitionAttribute(string)`.

Git evidence:

```
$ git status                    (before starting) → clean, on worktree-agent-a76f8e00527b8c25a
$ git add tests/TaskManagement.IntegrationTests/PostgresFixture.cs \
          tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs \
          tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs \
          tests/TaskManagement.IntegrationTests/TaskServiceTests.cs
$ git commit -m "test(integration): Testcontainers tests for schema, constraints, trigger and use cases" ...
[worktree-agent-a76f8e00527b8c25a 463a5d1] test(integration): Testcontainers tests for schema, constraints, trigger and use cases
 4 files changed, 833 insertions(+)
```

## `git diff --stat`

Against the immediate parent commit (`HEAD~1`, which is also the pre-B3 base `d831657` — this was B3's only
tests-commit):

```
$ git diff --stat HEAD~1
 tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs | 273 ++++++++++++++++++
 tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs   | 175 ++++++++++++
 tests/TaskManagement.IntegrationTests/PostgresFixture.cs         |  76 +++++
 tests/TaskManagement.IntegrationTests/TaskServiceTests.cs        | 309 +++++++++++++++++++++
 4 files changed, 833 insertions(+)
```

(This report file is added in a second commit on top, so it will not show under `HEAD~1` relative to itself; the
diff against the pre-B3 base commit `d831657` is identical to the above for the tests, plus this report file once
committed.)

## Branch and commit

- Worktree branch: `worktree-agent-a76f8e00527b8c25a` (`git rev-parse --abbrev-ref HEAD`)
- Tests commit SHA: `463a5d1ef226c272b710a84dc115374c49f4e6b7`
- Base commit (pre-B3): `d831657` (`docs(bus): wave B task specs for B1, B2 and B3`)

## `[uncertain]`

- **CA1001 does not fire** on `PostgresFixture`/the three test classes despite owning a disposable field and
  implementing only `IAsyncLifetime` — confirmed empirically by the clean stub build (0 warnings, 0 errors), not
  guessed. Spec flagged this as open; it is now resolved for this SDK/analyzer version.
- **Whether `ExecuteSqlRawAsync` surfaces `PostgresException` unwrapped**, as spec §15 states and as
  `DatabaseConstraintTests` codes it (`Assert.ThrowsAsync<PostgresException>`). The stub build only proves the code
  compiles against that assumption; it cannot prove the assumption itself, since the stub's `TaskManagementDbContext`
  never runs against a real database. This is exactly the "goes red without it" case the spec anticipates — it
  settles at fan-in B when the tests run for real. If `ExecuteSqlRawAsync` instead wraps in `DbUpdateException`, this
  file's `AssertRejectedAsync` helper and its 9 SQL-rejection tests will need `Assert.ThrowsAsync<DbUpdateException>`
  and to read `.InnerException as PostgresException` instead — a small, localized change.
- **Whether EF returns the tracked, stale `Employee` instance** in `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`
  (i.e., that `TaskService.CreateTaskAsync`'s own `SingleOrDefaultAsync` re-load of Bob, on the same `_dbContext` that
  already tracks him as active, returns the tracked instance rather than a value reflecting the row the raw-SQL
  `UPDATE` just committed). Spec §15 states this is `[uncertain] until the test runs`; unchanged here, since it
  cannot be resolved without a live database and a real `TaskService`. If EF instead re-queries and sees the
  deactivated row, the domain's own BR3 check (not just the trigger) would reject it first — the test's `RuleId ==
  "BR3"` assertion still holds either way, but the `InnerException` chain assertions (`DbUpdateException` →
  `PostgresException` with `ConstraintName == "trg_tasks_br3_assignee_active"`) would need loosening if the domain
  check fires instead of the trigger. This is exactly why the spec calls this test out as proving the race
  specifically — it is the one test most likely to need adjustment once B1/B2 land.
- Did not verify against a live PostgreSQL container in this task (none was started): all raw SQL statements were
  copied verbatim from spec §15, which states they were run in a throwaway `postgres:17-alpine` container by orch
  pre-implementation; I did not re-verify that run.

## Open questions

None blocking. The two items above are expected to settle empirically at fan-in B, as the spec anticipates, and
require at most small, localized edits to `DatabaseConstraintTests.AssertRejectedAsync` or to
`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`'s exception-chain assertions — not a redesign.
