---
title: "D3 code-reviewer → orch: review of the task-management diff"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[index]]", "[[log]]"]
from: D3-code-reviewer
to: orch
seq: 1
---

# D3 review, lens: code-reviewer

Scope: `git diff main...feature/task-management -- src tests .claude` (26 files, +3037). Read against
`ORCHESTRATOR_PROMPT.md`, `AGENTS.md`, `.wiki/domain/spec.md` and `docs/adr/0009-br3-br4-enforced-by-trigger.md`,
plus the evidence in the orch scratchpad `d3\` (`build.txt`, `test.txt`, `fa_red_evidence.txt`, `fb_red_evidence.txt`).
No worker bus reports were read. No code was edited.

I added one thing outside the evidence files: a throwaway console probe at
`C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\d3\probe\Program.cs`.
It references `TaskManagement.Application` and ran against a throwaway `postgres:17-alpine` container (removed
afterwards), migrated with `MigrateAsync`. Its output is quoted in F1 and F2.

**Counts:** critical 0 · major 1 · minor 1 · nit 2

## Findings

| id | severity | conf. | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| F1 | major | 88 | `src/TaskManagement.Application/TaskService.cs:82-96` | When the BR3 trigger rejects the insert, `CreateTaskAsync` turns it into `BusinessRuleViolationException("BR3", …, ex)` but leaves the rejected `TaskItem` tracked as `Added`. Every later `SaveChangesAsync` on the same `DbContext` tries that insert again. Result: (a) a **valid** `CreateTaskAsync` for an active assignee is rejected with a false BR3 that names the wrong employee; (b) `ChangeTaskStatusAsync` throws a raw `DbUpdateException`, which its `<exception>` docs (`:108-111`) do not list. A context lifetime of one operation hides this. The service, though, turns the failure into a business exception that tells the caller the error was handled, so a caller has no signal that the context is now unusable. The green suite misses it because `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` (`TaskServiceTests.cs:291-308`) makes the failing call the last one on that context. | Probe, the §15 race arrangement (Bob loaded, then deactivated by raw SQL), then two more calls on the same context:<br>`call 1 CreateTaskAsync(Race, Alice->Bob): BusinessRuleViolationException RuleId=BR3: BR3: Employee 10000000-0000-0000-0000-000000000002 is inactive …`<br>`tracked after call 1: TaskItem(Race)=Added, Employee(Bob Chen)=Unchanged, Employee(Alice Morgan)=Unchanged`<br>`call 2 CreateTaskAsync(Valid follow-up, Bob->Alice [Alice active]): BusinessRuleViolationException RuleId=BR3: BR3: Employee 10000000-0000-0000-0000-000000000001 is inactive and cannot be given a new task.` (Alice is active: seed §12 and `EmployeeConfiguration.cs:38`)<br>`call 3 ChangeTaskStatusAsync(20000000-...-0001, InProgress): DbUpdateException: … inner: 23514: BR3: employee 10000000-0000-0000-0000-000000000002 is inactive …` | In the `catch` block, before the `throw`: `dbContext.Entry(task).State = EntityState.Detached;`. Optional: add a `TaskServiceTests` step that makes a second, valid call on the same context after the race test's assertion. That step goes red without the detach. |
| F2 | minor | 75 | `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs:148,168` (claim in `docs/adr/0009-br3-br4-enforced-by-trigger.md`, Consequences) | The trigger runs on `BEFORE INSERT OR UPDATE OF status`, so BR3 is checked on insert only. A raw-SQL `UPDATE tasks SET assignee_id = <inactive employee>` is accepted. This **matches the approved spec** (§8: "Creation is the only moment `AssigneeId` is set … so BR3 is checked exactly there"; §11 SQL is copied verbatim). It contradicts ADR 0009, which says "Every writer is held to BR3 and BR4, raw SQL included." ADR 0009 lists "give a task to an inactive employee" as one of the raw-SQL problems it fixes. So this is a docs overstatement about a deliberate scope decision, not a worker deviation. | Probe 2, on the migrated seed database: `probe 2 raw UPDATE assignee_id -> Carol (inactive): rows=1; tasks now assigned to Carol=1` | Recommended: narrow the ADR 0009 sentence to "every insert is held to BR3; every status change to BR4, raw SQL included; `assignee_id` has no update path in the domain". Only if the human wants DB-level coverage of reassignment: add `assignee_id` to the trigger's `UPDATE OF` list and run the BR3 lookup when `TG_OP = 'INSERT' OR NEW.assignee_id IS DISTINCT FROM OLD.assignee_id`. That changes the approved schema, so it needs the human's approval (AGENTS.md rule 6). |
| F3 | nit | 60 | `src/TaskManagement.Application/TaskService.cs:1` | `using System.Threading;` is redundant: `ImplicitUsings` already imports `System.Threading`. The build does not flag it because IDE0005 is not configured. | `Directory.Build.props`: `<ImplicitUsings>enable</ImplicitUsings>`; `dotnet build -warnaserror --no-incremental` → `0 Warning(s)` | Delete the line. |
| F4 | nit | 50 | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:212,215` | Uses `DateTimeOffset.UtcNow`, but spec §6 says B3 uses fixed, whole-second times. No assertion reads the value, so the test cannot flake. It is a small deviation from the convention. | spec §6: "Use whole-second times in B3" | Use a fixed `new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)`. |

## Checked and found clean

- **Build:** `dotnet build -warnaserror --no-incremental` (re-run by me) → `Build succeeded. 0 Warning(s) 0 Error(s)`, exit 0.
  This matches `build.txt`.
- **Tests:** `test.txt` shows 54 unit and 34 integration tests, all passed. These counts match spec §15 row for row
  (A2: 15 + 19 + 20 = 54; B3: 4 + 13 + 17 = 34), and every test name matches §15.
- **Red evidence:** `fa_red_evidence.txt` / `fb_red_evidence.txt`. Removing any of these turns at least one test red:
  each domain guard (BR2–BR5), the BR1 assignment, each CHECK (BR1, BR2, BR5), the trigger, the service's BR3 check,
  or its trigger translation. After each removal the code was restored (`restored: True`).
- **Model and migration in sync:** `dotnet ef migrations has-pending-model-changes --project src/TaskManagement.Infrastructure`
  → `No changes have been made to the model since the last migration.` (exit 0). `migrations list --no-connect` shows
  exactly one migration, `20260918164447_InitialCreate` (spec §13: one migration only).
- **Fluent only (hard rule 2):** a regex search of `src/` for `[Key]`, `[Required]`, `[Table]`, `[Column]`,
  `[MaxLength]`, `[StringLength]`, `[ForeignKey]`, `[Index]`, `[NotMapped]`, `[DatabaseGenerated]`, `[Timestamp]`,
  `[ConcurrencyCheck]` and related attributes found nothing (grep exit 1). No `System.ComponentModel` usings.
  All mapping lives in the two `IEntityTypeConfiguration<T>` classes.
- **Schema against spec §10–§12:** `Up()` has exactly the §11 objects and nothing more: 2 PKs, 2 RESTRICT FKs, the
  4 named indexes (no stray `IX_…`), the 4 CHECKs (SQL identical to §11) and the trigger. The function SQL matches §11
  character for character. It sits at the end of `Up()` after the seed, and `Down()` starts with the drops.
  `xmin` is `xid` with `rowVersion: true`. The seed matches §12 exactly, status is stored as the strings
  `"New"`, `"Completed"`, `"Cancelled"`, and employees are inserted before tasks.
- **Domain against spec §3:** guard order in `TaskItem.Create` is title, then null checks, then UTC normalisation,
  then BR5, BR3, BR2, matching §3.2. The messages and `RuleId`s match. `ChangeStatus` follows §3.2 steps 1–4 and the
  full §5 transition table. Every property has a `private set`, each entity has a private parameterless constructor,
  there are no navigations, and keys come from `Guid.CreateVersion7()`. `BusinessRuleViolationException` has the §3.4
  shape.
- **UTC:** every timestamp is `DateTimeOffset`/`timestamptz`. The domain normalises with `ToUniversalTime()` and never
  reads a clock. The service takes "now" from `TimeProvider.GetUtcNow()`. The only wall-clock reads are the F4 nit.
- **Async and CancellationToken:** all three service methods are `async`, take a `CancellationToken`, and forward it
  to every EF call. `PostgresFixture.CreateDatabaseAsync` forwards its token to `MigrateAsync`.
- **Service against spec §14:** signatures, lookup order, `KeyNotFoundException` messages, the service's BR3 check
  before the domain call, and the BR3 catch filter all match (`SqlState == PostgresErrorCodes.CheckViolation` and
  `ConstraintName == "trg_tasks_br3_assignee_active"`, the equality form, the inner exception kept). Other
  `DbUpdateException`s propagate. `DbUpdateConcurrencyException` is not caught. The list query uses `AsNoTracking`,
  the optional status filter and `OrderBy(DueAt).ThenBy(Id)`, and the §14 `[uncertain]` about NULLS LAST is settled
  by the passing `ListTasksByAssigneeAsync_Bob_ReturnsHisTasksOrderedByDueAt`.
- **Tests:** all 67 test methods carry a `Category` trait (Unit or Integration). No in-memory provider is referenced.
  Integration tests use Testcontainers with `postgres:17-alpine` and a fresh migrated database per test.
- **Packages:** versions match the AGENTS.md stack table. No package appears that the spec does not approve.
- **`.claude/` (hooks):** the human asked for it (`.wiki/log.md:211`), so it is not out of scope. On
  code.claude.com/docs/en/hooks, `shell` ("Accepts `"bash"` or `"powershell"`"), `statusMessage`, `PreCompact`,
  `PostCompact`, `SessionStart` and `StopFailure` are all documented. The script's `graph_statuses` parser, run
  against the real `graph.yaml`, returns all 15 nodes. `[uncertain]`: the input field names `compact_summary`
  (PostCompact) and `error` (StopFailure) were not in the docs excerpt I fetched. The script falls back safely when
  either is missing.
- **Scope:** no web API, UI or host. The only files outside `src/` and `tests/` are the requested hooks.
