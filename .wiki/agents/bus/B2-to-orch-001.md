---
title: "B2 → orch: application service report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[br3-inactive-assignee]]", "[[index]]"]
from: B2
to: orch
seq: 1
---

# B2 Application service: report

## Status

Done. `TaskService` implements the three §14 use cases exactly as specified. The worktree cannot compile it
against the real `TaskManagementDbContext` (B1's file does not exist here yet), so correctness was checked with
an out-of-repo stub built from spec §13 (constructor + two `DbSet` properties only). That stub build is
`dotnet build -warnaserror` clean: 0 warnings, 0 errors. Real compilation against B1's DbContext happens at
fan-in B.

## Files changed

- `src/TaskManagement.Application/TaskService.cs` (new)
- `.wiki/agents/bus/B2-to-orch-001.md` (this report, new)

## Public API implemented

```csharp
namespace TaskManagement.Application;

public sealed class TaskService
{
    public TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider);

    public Task<TaskItem> CreateTaskAsync(
        string title,
        Guid creatorId,
        Guid assigneeId,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? dueAt,
        CancellationToken cancellationToken = default);

    public Task<TaskItem> ChangeTaskStatusAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<TaskItem>> ListTasksByAssigneeAsync(
        Guid assigneeId,
        TaskItemStatus? status = null,
        CancellationToken cancellationToken = default);
}
```

No interface (spec §14: "one implementation, no mocking need"). Constructor stores `dbContext` and
`timeProvider` in `private readonly` fields (IDE0044).

## §14 step → code line

All line numbers refer to the committed `src/TaskManagement.Application/TaskService.cs`.

### `CreateTaskAsync`

| §14 step | Code |
|---|---|
| 1. Load creator; null → `KeyNotFoundException` | lines 61–65 |
| 2. Load assignee; null → `KeyNotFoundException` | lines 67–71 |
| 3. BR3 service check (`!assignee.IsActive`) | lines 73–78 |
| 4. `TaskItem.Create(...)` (domain re-checks BR5, BR3, BR2) | line 80 |
| 5. `Add` + `SaveChangesAsync`; return `task` | lines 82, 86, 98 |
| 6. BR3 race: catch `DbUpdateException` on the `PostgresException`/`CheckViolation`/`trg_tasks_br3_assignee_active` pattern, rethrow as `BusinessRuleViolationException("BR3", …, ex)`; every other `DbUpdateException` propagates | lines 84–96 |

### `ChangeTaskStatusAsync`

| §14 step | Code |
|---|---|
| 1. Load task; null → `KeyNotFoundException` | lines 118–122 |
| 2. `task.ChangeStatus(newStatus, timeProvider.GetUtcNow())` (BR4, BR1) | line 124 |
| 3. `SaveChangesAsync`; `DbUpdateConcurrencyException` not caught, propagates | line 126 |
| 4. Return `task` | line 128 |

### `ListTasksByAssigneeAsync`

| §14 step | Code |
|---|---|
| `status` has a value that is not a defined member → `ArgumentOutOfRangeException` | lines 147–150 |
| `dbContext.Tasks.AsNoTracking().Where(t => t.AssigneeId == assigneeId)` | line 152 |
| `.Where(t => t.Status == status.Value)` only when `status` has a value | lines 154–157 |
| `.OrderBy(t => t.DueAt).ThenBy(t => t.Id).ToListAsync(cancellationToken)` | line 159 |

`CancellationToken` is forwarded to every async EF call: `SingleOrDefaultAsync` (×2 in `CreateTaskAsync`, ×1 in
`ChangeTaskStatusAsync`), `SaveChangesAsync` (×2), `ToListAsync` (×1) — CA2016 satisfied.

No `ArgumentNullException.ThrowIfNull` was added in `TaskService`: every parameter is either a `Guid`/enum value
type, or `title`, whose null/blank/length validation the spec assigns to `TaskItem.Create` (§14 step 4, domain
already covers it — matches the task brief's "Validate `ArgumentNullException.ThrowIfNull` only where the spec
implies it").

## Stub build (out-of-repo compile check)

Stub folder (outside the repo, never committed):
`C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\b2-stub\`

Contents: `B2Stub.csproj` (same four package references as
`src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj`: `Microsoft.EntityFrameworkCore`,
`Microsoft.EntityFrameworkCore.Relational`, `Microsoft.EntityFrameworkCore.Design`,
`Npgsql.EntityFrameworkCore.PostgreSQL`, all 10.0.12/10.0.3 as in the real csproj), a copy of
`Directory.Build.props` and `.editorconfig` from the repo root, copies of the four domain files
(`Employee.cs`, `TaskItem.cs`, `TaskItemStatus.cs`, `BusinessRuleViolationException.cs`), a stub
`TaskManagement.Infrastructure/TaskManagementDbContext.cs` (constructor + `DbSet<Employee> Employees` +
`DbSet<TaskItem> Tasks` only, per spec §13, marked as a stub in its own doc comment), and the committed
`TaskService.cs` verbatim.

```
$ cd b2-stub && rm -rf bin obj && dotnet build -warnaserror
  Determining projects to restore...
  Restored C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\b2-stub\B2Stub.csproj (in 567 ms).
  B2Stub -> C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\b2-stub\bin\Debug\net10.0\B2Stub.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.24
```

**Sanity check that the doc/analyzer pipeline is genuinely enforced** (not silently skipped in the stub): with
the class's XML doc comment block deleted entirely, the same `dotnet build -warnaserror` failed with
`error CS1591: Missing XML comment for publicly visible type or member 'TaskService'`. The file was restored
immediately after (verified with `diff` against the repo's `TaskService.cs`: identical) and the clean build
above is the final, correct result.

```
$ diff "TaskManagement.Application/TaskService.cs" "<repo>/src/TaskManagement.Application/TaskService.cs"
(no output — files identical)
```

## Npgsql check (`PostgresException`, `PostgresErrorCodes.CheckViolation`)

Confirmed two ways:
1. **Stub build**: `TaskService.cs` references `Npgsql.PostgresException` and `Npgsql.PostgresErrorCodes.CheckViolation`
   directly (the `catch (DbUpdateException ex) when (...)` filter, line 88–90) and the stub build above compiled
   with 0 errors against the restored `Npgsql` 10.0.3 package — the compiler itself resolved both symbols.
2. **Package inspection** (`~/.nuget/packages/npgsql/10.0.3/lib/net10.0/`):
   ```
   $ grep -n "PostgresException\b" Npgsql.xml | head -5
   7439:        <member name="T:Npgsql.PostgresException">
   ...
   $ grep -ni "PostgresErrorCodes" Npgsql.xml
   7431:        <member name="T:Npgsql.PostgresErrorCodes">
   7498:            Constants are defined in <seealso cref="T:Npgsql.PostgresErrorCodes"/>.
   ```
   The XML docs confirm both types exist but do not carry per-field doc comments for `PostgresErrorCodes`
   members (no XML `<member>` entry for the `CheckViolation` field specifically) — the compiler check above is
   the authoritative confirmation for that field.

## Commands run (real output)

```
$ find ~/.nuget/packages/npgsql/10.0.3/lib -maxdepth 2 -type d
/c/Users/mcgun/.nuget/packages/npgsql/10.0.3/lib
/c/Users/mcgun/.nuget/packages/npgsql/10.0.3/lib/net10.0
/c/Users/mcgun/.nuget/packages/npgsql/10.0.3/lib/net8.0
/c/Users/mcgun/.nuget/packages/npgsql/10.0.3/lib/net9.0
```

```
$ cd b2-stub && dotnet build -warnaserror   # first pass, before the CS1591 sanity check
  Determining projects to restore...
  Restored ...\B2Stub.csproj (in 300 ms).
  B2Stub -> ...\bin\Debug\net10.0\B2Stub.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.54
```

```
$ # class doc comment deleted entirely
$ dotnet build -warnaserror
...TaskService.cs(9,21): error CS1591: Missing XML comment for publicly visible type or member 'TaskService'
Build FAILED.
    0 Warning(s)
    1 Error(s)
```

```
$ # restored, rm -rf bin obj, rebuild
$ dotnet build -warnaserror
  Determining projects to restore...
  Restored ...\B2Stub.csproj (in 567 ms).
  B2Stub -> ...\bin\Debug\net10.0\B2Stub.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.24
```

```
$ git status
On branch worktree-agent-a0033f50f087cee5d
Untracked files:
  (use "git add <file>..." to include in what will be committed)
	src/TaskManagement.Application/TaskService.cs

nothing added to commit but untracked files present (use "git add" to track)
```

Evidence of this commit itself (`git diff --stat HEAD~1`, branch, SHA) is filled in below after committing —
a report committed as part of the history it describes cannot show its own commit's stat in advance (same
constraint A1 hit; see `.wiki/agents/bus/A1-to-orch-001.md`).

## `git diff --stat HEAD~1`, branch and commit SHA

```
$ git diff --stat HEAD~1
 .wiki/agents/bus/B2-to-orch-001.md            | 234 ++++++++++++++++++++++++++
 src/TaskManagement.Application/TaskService.cs | 161 ++++++++++++++++++
 2 files changed, 395 insertions(+)
```

```
$ git rev-parse --abbrev-ref HEAD
worktree-agent-a0033f50f087cee5d

$ git rev-parse HEAD
77d1a9f08fac793e77b9c9a2ed3d28bb85b1c8b1
```

Only `TaskService.cs` and this report changed, as required. Commit `77d1a9f`:
`feat(application): task service for the three use cases`.

## [uncertain] items

- `Npgsql.PostgresErrorCodes.CheckViolation`'s exact CLR kind (`const string` vs `static readonly string`) is
  `[uncertain]` from documentation alone — the XML docs have no per-field entry for it. Spec §14 anticipated this
  exact gap ("The equality form (not a property pattern) is deliberate: it compiles whether `CheckViolation` is a
  `const` or a `static readonly` field."), and the code uses `==` accordingly. The stub build's clean compile is
  the authoritative confirmation that the symbol exists and the equality comparison against `pg.SqlState`
  (`string`) type-checks.

## Open questions

None. Spec §14, §13, §6, §8, §2a and the two ADRs (0008, 0009) fully determined every signature, guard, catch
filter and exception type; nothing was ambiguous enough to need a human decision.
