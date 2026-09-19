---
title: "D3 → orch: silent-failure-hunter re-review of the D4 fixes"
type: bus-message
status: sent
updated: 2026-09-19
related: ["[[orch-to-D3-002]]", "[[orch-to-D4-001]]", "[[D3-to-orch-004]]", "[[spec]]", "[[log]]"]
from: D3-silent-failure-hunter
to: orch
seq: 104
---

# D3 re-run, lens: silent-failure-hunter

Scope: `git diff ae7bb74 5b0650c -- src tests docs/adr/0009-br3-br4-enforced-by-trigger.md`, checked against
`orch-to-D4-001.md`, my `D3-to-orch-004.md` and spec §14–§15.

**Headline:** the two new `finally { if (!saved) … Detached }` blocks close SF1 and SF2. They catch nothing and
swallow nothing, and they do not change which exception the caller gets: the translated BR3, the FK error, the
transient error, the concurrency error and cancellation all come out unchanged. Cancellation is handled correctly.
D4 introduced one regression: by the time the caller's `catch` runs, the `finally` has already detached the entry in
the exception's `Entries`. Microsoft's documented client-wins handling of a `DbUpdateConcurrencyException` now
**writes nothing and raises nothing**; before D4 it persisted the change. SF3 is still open for the case its
original text named.

**Evidence method.** This is the same approach as D3, run outside the repo. `git archive 5b0650c` (and, for the
baseline, `ae7bb74`) of `src` plus `Directory.Build.props` goes into the scratchpad and is built there. Two probe
consoles, identical `Program.cs` whose only difference is which DLLs they reference, run against `postgres:17-alpine`
(Testcontainers 4.15.0). Each scenario gets a fresh database with the real `InitialCreate` applied. Source and output:
`C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\d4probe\{Program.cs,probe_output.txt}`
(D4 build) and `...\scratchpad\preD4probe\probe_output.txt` (ae7bb74 build, scenarios N4a, N4b and R8 only).
Scenarios R1–R8 re-run D3's P1–P8. N1–N5 are new. No repo file was edited and nothing was built inside the repo.

## 1. Earlier findings

| id | orch disposition | verdict | code line | evidence |
|---|---|---|---|---|
| SF1 | fixed | **verified** | `src/TaskManagement.Application/TaskService.cs:93-115` (detach at `:113`) | R1: after the BR3 race, `tracker after failure: pending=[] trackedTasks=[]`, and the next `ChangeTaskStatusAsync` returns `OK` (before the fix it threw a raw `DbUpdateException`). R3: `'Race' rows=0` after Bob is reactivated (before: 1). R4: a retry after a transient error gives `'Retry' rows=1` (before: 2). Red evidence: removing the detach turns `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` (`TaskServiceTests.cs:341`) red. |
| SF2 | fixed | **verified** | `TaskService.cs:150-163` (detach at `:161`) | R5: `A#1: DbUpdateConcurrencyException`, `tracker after failure: pending=[] trackedTasks=[]`, `A#2 retry Cancelled: OK`, `db: task 0001 status=Cancelled`, and `A create: OK` with `'AfterConflict' rows=1` (before: a false BR4, and `DbUpdateConcurrencyException` from the unrelated create). Red evidence: `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds` (`TaskServiceTests.cs:365`). |
| SF3 | "resolved by the SF1 fix" | **not fixed** (my reproduction is closed; the residual case named in SF3 is not) | `TaskService.cs:99-105` (filter and message unchanged) | R2 (= P2): `create#2: OK`, so a stale task the service left behind can no longer cause the misattribution. SF3 also said: "after SF1's fix it can still happen if the caller has its own pending `TaskItem` insert on the shared context". **N5** shows this: the caller `Add()`s its own `TaskItem` for a stale-active Bob, then calls `CreateTaskAsync(creator Bob, assignee Alice)` and gets `BR3: Employee 10000000-…-0001 is inactive` (Alice, who **is** active). The inner exception is `23514/trg_tasks_br3_assignee_active`, and the caller's entity correctly stays `TaskItem[Added]`. The triage's reason ("its reproduction needed a stale `Added` task from an earlier failed call") covers only P2. Severity stays **minor**, and it is reachable only when the caller writes to the context outside the service. Smallest fix as before: add `&& ex.Entries.Count == 1 && ReferenceEquals(ex.Entries[0].Entity, task)` to the filter. The filter runs before the `finally`, so the detach does not affect it. That changes the spec §14 step 6 filter, so the decision is orch's. The alternative is a `<remarks>` line on `CreateTaskAsync`: "the BR3 translation assumes this call's task is the only pending insert". |
| SF4 | fixed | **verified** | `TaskService.cs:56`, `:131`, `:132`, `:180` | `<exception cref="OperationCanceledException">` is on all three methods (56, 132, 180). `<exception cref="DbUpdateException">` is on `ChangeTaskStatusAsync` (131). N1a and N2a confirm that cancellation surfaces as a bare `OperationCanceledException`, not wrapped. |
| SF5 | fixed | **verified** | `TaskService.cs:52-55`; test `TaskServiceTests.cs:396` | The doc names the FK case ("the foreign key when an employee is deleted between the read and the insert"). R7: `DbUpdateException -> PostgresException(23503/fk_tasks_employees_assignee_id)`, untranslated, tracker empty. Red evidence: widening the filter turns only `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException` red. |

## 2. New findings introduced by D4

| id | severity | conf | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| SF6 | major (**regression**: worked before D4) | 90 | `src/TaskManagement.Application/TaskService.cs:156-162` (and `:108-115`); doc `:130` | A `finally` runs while the exception unwinds, **before** the caller's `catch`. So the `DbUpdateConcurrencyException` that spec §14 step 3 hands to the caller "to handle" arrives with its `Entries` already `Detached`. EF Core's documented client-wins handling of that exception (`entry.OriginalValues.SetValues(await entry.GetDatabaseValuesAsync())`, then `SaveChangesAsync`) now returns **0** and throws nothing, so the caller's change is silently dropped. The detach also takes an instance the caller may own: when the caller already tracked the task, identity resolution makes `SingleOrDefaultAsync` (`:142`) return the caller's instance, and the failure detaches it. Later saves of that instance through the caller's own `SaveChanges` then write nothing. `CreateTaskAsync` shows the same effect (`DbUpdateException.Entries` is `Detached`), but there it is harmless: the service created that task itself. The code matches the amended spec exactly: §14 step 7 says "any exception, translated or not" and "in a `finally`", and step 3 says the same for `ChangeTaskStatusAsync`. So this is a gap in the prescribed pattern, not a deviation by D4, and any code fix requires a spec amendment. | A/B with identical probe code, only the DLLs differ. **Pre-D4 (ae7bb74), N4a:** `Entries count=1 states=[Modified]` → `SetValues: OK` → `SaveChangesAsync after resolution: returned 1` → `db: task 0001 status=Cancelled`. **D4 (5b0650c), N4a:** `states=[Detached]` → `SetValues: OK; entry.State=Detached` → `SaveChangesAsync after resolution: returned 0, no exception` → `db: task 0001 status=InProgress`. Control **N4b** (database wins: `entry.ReloadAsync()` then a retry through the service) works the same before and after (`ReloadAsync: OK; entry.State=Unchanged`, `retry through service: OK`, `status=Cancelled`), so D4 broke only one of the two documented strategies, and broke it silently. **R8:** the translated BR3's inner `DbUpdateException.Entries` is `states=[Detached]` after D4 and `[Added]` before. The only mention is the method `<remarks>` (`:134-135`, "the task is detached"). The `<exception cref="DbUpdateConcurrencyException">` text (`:130`) does not say that `Entries` is unusable. No test touches `Entries` (`rg "Entries" tests` finds nothing). | Keep the detach: SF2 needs it, and skipping it for `DbUpdateConcurrencyException` would bring back the false BR4. Do **not** move a `Reload` into the `finally`: an awaited call there can throw and replace the original exception. Doc-only fix: extend `:130` to "…since it was read. The task is detached before this exception propagates, so its `Entries` are detached: recover by calling this method again. Resolving `Entries` and calling `SaveChanges` writes nothing." Optionally, one test: after the conflict, assert `ex.Entries.Single().State == EntityState.Detached`, which pins the documented contract. If orch wants the client-wins route to work instead, that is a design change (a separate refresh path in place of the detach) that amends spec §14 steps 3 and 7, so it needs its own decision. The doc-only fix is the only option that leaves the approved contract untouched. |
| SF7 | nit | 90 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs` (no line: nothing covers it) | No test covers the cleanup on the cancellation path. Cancellation reaches the cleanup only through `finally`, because `OperationCanceledException` is not a `DbUpdateException`. A refactor that narrows `finally` to `catch (DbUpdateException) { detach; throw; }` would keep all 38 integration tests green and silently bring SF1/SF2 back for canceled saves. (That is an inference, not a mutation run: both detach tests fail through a `DbUpdateException` or a subclass of it, and no test cancels a token.) The current code is correct. | N1a: `create: OperationCanceledException` (no inner exception), `tracker after failure: pending=[] trackedTasks=[]`, `'Cancel1' rows=0`, `next create (same ctx): OK`. N2a: the same for `ChangeTaskStatusAsync` (`status=New`, then `retry (same ctx): OK` → `Completed`). `rg "OperationCanceled\|CancellationTokenSource\|\.Cancel\(" tests` finds nothing. | One test: cancel a `CancellationTokenSource` from a command interceptor just before `INSERT INTO tasks`, assert `OperationCanceledException`, then assert that the next `CreateTaskAsync` on the same context succeeds and no row exists for the canceled title. |

Counts (new): critical 0, major 1, minor 0, nit 1. Carried over as not fixed: SF3 (minor).

## Checked and found clean

- **The `finally` does not hide, swallow or mistranslate anything.** It has no `catch`, and the only statement it runs
  on failure is the state assignment. In every failure the probe exercised, the caller got the same exception as
  before D4: translated BR3 with the chain `BusinessRuleViolationException -> DbUpdateException -> PostgresException(23514/trg_tasks_br3_assignee_active)`
  (R1, R8), FK `23503` (R7), transient `NpgsqlException` inside `DbUpdateException` (R4), `DbUpdateConcurrencyException`
  (R5, N4a) and a bare `OperationCanceledException` (N1a, N2a). The `saved` flag stays false on every one of those
  paths and becomes true only after `SaveChangesAsync` returns.
- **Translated BR3 path.** The `when` filter is evaluated before the `finally` runs, and it reads only
  `ex.InnerException`, so the detach cannot change whether the filter matches. The catch body throws BR3, then the
  `finally` detaches: R8 shows BR3, an empty tracker and `'Race8' rows=0`. The ordering is correct.
- **Cancellation.** When the token is canceled before the command is sent (N1a, N2a), the result is
  `OperationCanceledException`, the entity is detached, nothing is persisted, and the next call on the same context
  succeeds. When it is canceled after the INSERT or UPDATE executed (N1b, N2b), just before COMMIT (N1c) or after
  COMMIT (N1d), the save completes with no exception: `saved = true`, the entity is `Unchanged` and the database
  matches the returned entity. None of those cases reported "canceled" for a committed row. N2b's follow-up BR4 comes
  from a task that really was `Completed`, not a false BR4. `[uncertain]`: a cancellation that arrives while COMMIT is
  on the wire (Npgsql sends a cancel request) could in principle commit on the server and still throw. I could not
  trigger it with interceptors. If it happens, the detach is the right reaction (nothing is re-sent).
- **Only the service's own entity is detached.** N5: the caller's pending `TaskItem` stays `Added`, and the service
  does not discard caller state it never touched. (The caveat about a caller-*tracked* task in `ChangeTaskStatusAsync` is
  part of SF6.)
- **The domain is doc-only, as orch stated.** `git diff ae7bb74 5b0650c -- src | grep '^[+-]' | grep -v '///'`
  returns only the removed `using System.Threading;` and the two `saved`/`finally` blocks.
- **New tests contain no swallowing.** The test diff has no `try`/`catch`/`finally`, `Record.Exception` or `Skip`.
  `DeleteEmployeeBeforeSaveInterceptor` (`TaskServiceTests.cs`, end of file) passes `cancellationToken` through and
  returns `result` unchanged.
- **ADR 0009** now limits the claim to "BR4 and BR3 **on insert**" and names the gap (a raw-SQL `UPDATE` of
  `assignee_id`) as deferred to the human. That is a documented limit, not a hidden one.
