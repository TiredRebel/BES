---
title: "D3 → orch: silent-failure-hunter review"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[index]]", "[[log]]"]
from: D3-silent-failure-hunter
to: orch
seq: 4
---

# D3 review, lens: silent-failure-hunter

Scope: `src/TaskManagement.Application/TaskService.cs`, plus the persistence layer where it affects which error the
caller sees (`src/TaskManagement.Infrastructure/**`, the trigger in `InitialCreate`). Judged against spec §5, §8, §11,
§14 and ADR 0009. I did not read the workers' bus reports.

**Headline:** the BR3 catch filter itself is correct, narrow and proven, and no error in `src/` is caught and
dropped. The defects are **mistranslation and deferred persistence**, and they come from **what a failed save leaves
behind in the `DbContext`**. `TaskService` never cleans up
the change tracker when `SaveChangesAsync` throws. On a context that is reused afterwards (the service takes the
context by constructor, and nothing in the repo fixes its lifetime), a failed operation carries over into later ones:
it gets persisted silently, persisted twice, or reported as the wrong error against the wrong employee or rule. The
spec does not cover this (§14 prescribes the catch shape and says nothing about tracker state), so these findings are
gaps in the spec, not deviations from it.

**Evidence method.** I ran a scratch console probe outside the repo against a real `postgres:17-alpine` container
(Testcontainers 4.15.0). It references the built `TaskManagement.{Domain,Infrastructure,Application}.dll` from
`tests/TaskManagement.IntegrationTests/bin/Debug/net10.0/` (built 23:31, newer than the sources at 23:20) and applies
the real `InitialCreate` migration to a fresh database for each scenario. Source:
`C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\d3probe\Program.cs`;
output: `...\scratchpad\d3probe\probe_output_clean.txt`. Scenario ids P1–P10 below refer to that output. SF1 and SF2
need a caller that reuses a context after an exception. Nothing in the repo does that today (there is no DI
registration or API layer), so both are rated major, not critical. No repo file
was edited and nothing was built inside the repo.

## Findings

| id | severity | conf | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| SF1 | major | 95 | `src/TaskManagement.Application/TaskService.cs:82-96` | When `SaveChangesAsync` throws in `CreateTaskAsync` (the translated BR3, any other `DbUpdateException`, or a cancellation), the new `TaskItem` stays **`Added`** in the context. The next `SaveChanges` on that context, from any operation, sends the old insert again. Result: a task the caller was told was rejected gets persisted later without anyone being told, a retry after a transient error creates a **duplicate** task, or an unrelated later call fails with this call's error. | Code: `dbContext.Tasks.Add(task);` (82), then the `try`/`catch` (84-96) has no cleanup on any failure path. Probe: **P1** `tracker after failure: TaskItem[Added]`; the next `ChangeTaskStatusAsync(0001, InProgress)` on the same context throws a raw `DbUpdateException -> PostgresException(23514/trg_tasks_br3_assignee_active)`, which that method neither translates nor documents, and its own change is lost (`task 0001 status=New`). **P3** the caller gets BR3 for task "Race"; Bob is reactivated; an unrelated status change then commits "Race" (`'Race' rows=1 (the caller was told BR3 rejected it)`). **P4** the first insert fails with a simulated transient `NpgsqlException`; a retry of the same `CreateTaskAsync` returns OK and `'Retry' rows=2`. **P6** (control): the same sequence on a fresh context leaves no rows and raises no error. No test covers this: the race test re-reads through a *second* context (`tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:306-307`) and never reuses `_dbContext` after the failure. | Detach the entity on every failure path. The smallest form keeps the spec's filter unchanged and adds `finally { var entry = dbContext.Entry(task); if (entry.State == EntityState.Added) { entry.State = EntityState.Detached; } }` to the existing `try` (on success the state is `Unchanged`, so this runs only after a failure). Add one test: after the BR3-race assert, call `CreateTaskAsync` again on the same `_dbContext` for an active assignee and assert that it succeeds and that no "Race" row exists. The minimum fallback is a `<remarks>` saying the context must be discarded after any exception, which is weaker. |
| SF2 | major | 95 | `src/TaskManagement.Application/TaskService.cs:118-126` | After a `DbUpdateConcurrencyException`, the task stays **`Modified`** in the context and keeps the status the caller *tried* to set. The obvious reaction to a concurrency error is to retry, and a retry on the same context gets the tracked instance back from `SingleOrDefaultAsync` (identity resolution: the query does not overwrite a tracked entity's values). The domain then rejects the retry with a **false BR4**. The concurrency conflict is misreported as a business-rule violation, the task in the database is not final, and every later save on the context re-sends the stale update. | Code: `var task = await dbContext.Tasks.SingleOrDefaultAsync(...)` (118), `task.ChangeStatus(...)` (124), and `await dbContext.SaveChangesAsync(cancellationToken);` (126), with no cleanup when 126 throws. Probe **P5**: context A moves 0001 New→InProgress; B moves it back to New; A's attempt to set Cancelled throws `DbUpdateConcurrencyException` (correct, per spec §14 step 3) and leaves `tracker after failure: TaskItem[Modified]`. A retries Cancelled and gets `BusinessRuleViolationException(RuleId=BR4, "Task …0001 is Cancelled; Completed and Cancelled are final.")`; a retry to InProgress gets the same BR4; the database says `task 0001 status=New`. A's next `CreateTaskAsync` then throws `DbUpdateConcurrencyException` (not in that method's `<exception>` list) and persists nothing (`'AfterConflict' rows=0`). | Same pattern: wrap line 126 in `try { … } finally { var entry = dbContext.Entry(task); if (entry.State == EntityState.Modified) { entry.State = EntityState.Detached; } }`, so a retry re-reads the row. Add one test: after `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`'s assert, retry through `TaskService` on context 2 and assert that it does not throw BR4 while the row is not final. |
| SF3 | minor | 90 | `src/TaskManagement.Application/TaskService.cs:88-95` | The filter matches the BR3 trigger error from **any** pending insert in the save, but the translated message always names this call's `assigneeId`. When the rejected row belongs to a different task, the caller is told the wrong employee is inactive. SF1 makes this reachable today; after SF1's fix it can still happen if the caller has its own pending `TaskItem` insert on the shared context. | Code: `pg.ConstraintName == "trg_tasks_br3_assignee_active"` (90), then `$"BR3: Employee {assigneeId} is inactive …"` (94). Probe **P2**: after a BR3 race on Bob, `CreateTaskAsync(creator Bob, assignee Alice)` on the same context throws `BR3: Employee 10000000-…-0001 is inactive` (Alice, who **is active**), while the inner `PostgresException` says `employee 10000000-…-0002` (Bob). **P9**: `DbUpdateException.Entries` lists **both** rows (`Race/assignee=…0002, ForAlice/assignee=…0001`) and `PostgresException.BatchCommand` is null, so adding `ex.Entries.Any(e => e.Entity == task)` to the filter would **not** fix it. **P10**: in the normal single-insert BR3 race (both the stale-tracking and the real-interleaving variants), `Entries.Count=1 sameAsTask=True`, so the fix below keeps the spec'd translation working. | Fix SF1 first. Then add `&& ex.Entries.Count == 1 && ReferenceEquals(ex.Entries[0].Entity, task)` to the filter, so a trigger error that cannot be tied to this task propagates as the raw `DbUpdateException` instead of blaming `assigneeId`. P10 shows the clean-context race still matches this filter. |
| SF4 | minor | 90 | `src/TaskManagement.Application/TaskService.cs:43-51`, `:108-111`, `:141` | The `<exception>` lists are incomplete against spec §14 ("Exceptions the service lets through, all documented with `<exception>`: … `DbUpdateConcurrencyException`, any other `DbUpdateException`, `OperationCanceledException`"). None of the three methods documents `OperationCanceledException`. `ChangeTaskStatusAsync` documents only `DbUpdateConcurrencyException`, but a plain `DbUpdateException` can escape its save. Callers who code against the docs will not handle these. (This overlaps the comment-analyzer lens; I include it because it changes what callers expect to catch.) | `rg "OperationCanceledException" src` finds nothing in `TaskService.cs`. The only `DbUpdateException` doc is line 51, on `CreateTaskAsync`. Probe **P1** shows `ChangeTaskStatusAsync` throwing a raw `DbUpdateException`. The same happens for any database or connection error during its save. | Add `<exception cref="OperationCanceledException">` to all three methods and `<exception cref="DbUpdateException">` to `ChangeTaskStatusAsync`. Once SF2's fix lands, `CreateTaskAsync` needs no `DbUpdateConcurrencyException` entry. |
| SF5 | nit | 90 | `src/TaskManagement.Application/TaskService.cs:51`, `:61-71` | The not-found case is reported two different ways. An employee missing at read time gives `KeyNotFoundException`; an assignee deleted between the read and the insert gives a raw `DbUpdateException` (FK `23503`). A caller that maps `KeyNotFoundException` to "not found" handles the race as an unknown server error. Spec §14 step 6 allows this ("Every other `DbUpdateException` propagates unchanged"), which is why this is only a nit. | Probe **P7**: employee Dan is deleted by another connection right before the INSERT, and the call fails with `DbUpdateException -> PostgresException(23503/fk_tasks_employees_assignee_id)`. The doc on line 51 says only "a reason other than the BR3 trigger". | Doc-only: extend line 51 with "including `23503` when the creator or assignee was deleted after it was read". No code change: the spec deliberately limits translation to BR3. |

Counts: critical 0, major 2, minor 2, nit 1.

## Checked and found clean

- **No swallowed errors in `src/`.** `rg "catch" src` (migrations excluded) finds exactly one `catch`, at
  `TaskService.cs:88`. It has no empty body, no catch-all, no log-and-continue and no null or default return on
  error. The domain's only `?.` calls (`TaskItem.cs:83-84`) normalise nullable dates and hide nothing.
- **BR3 catch filter is correct and narrow.** `TaskService.cs:88-90` matches spec §14 step 6 character for character.
  It is a `when` filter (no unwinding when it does not match) that needs all three predicates: type, SqlState
  `23514`, and the constraint name. Every other `DbUpdateException` propagates unchanged: **P4** (a transient
  `NpgsqlException` stays `DbUpdateException`) and **P7** (FK `23503` stays `DbUpdateException`).
- **The inner chain is preserved.** BR3 → `DbUpdateException` → `PostgresException(23514/trg_tasks_br3_assignee_active)`
  (**P1**, **P8**); the repo test asserts the same chain (`TaskServiceTests.cs:301-304`).
- **The translation is load-bearing and works under a real interleaving.** `fb_red_evidence.txt:19-20`: removing the
  translation turns `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` red. **P8** deactivates Bob
  on another connection between the service's read and its INSERT (not the stale-tracking trick the repo test uses):
  the result is BR3 and `'Race8' rows=0`.
- **The trigger does not mask referential errors.** `InitialCreate.cs:151-152`: `SELECT … INTO assignee_active … FOR
  SHARE` leaves NULL for an unknown assignee, and `IS FALSE` is false for NULL, so the FK reports it. **P7** returns
  `23503/fk_tasks_employees_assignee_id`, not the BR3 trigger error. That is a deliberate design choice and it works.
- **Trigger errors carry their names.** `USING ERRCODE = 'check_violation', CONSTRAINT = '…'`
  (`InitialCreate.cs:154`, `:159`) fills `PostgresException.ConstraintName` (**P1**), which is what the filter needs.
- **BR4 race: concurrency error before the trigger.** An EF update carries `xmin`, so a row changed by another writer
  fails the concurrency check, and the exception is deliberately not caught (spec §14 step 3, ADR 0009). **P5 A#1**
  and the passing `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` (`test.txt:127`) confirm it.
  The problem is what happens after that exception (SF2), not the exception itself.
- **Not-found paths are explicit.** `KeyNotFoundException` names the id for the creator (64), the assignee (70) and the
  task (121); none of these paths returns null.
- **`ListTasksByAssigneeAsync`** rejects an undefined status with `ArgumentOutOfRangeException` (147-150) instead of
  quietly returning an empty list. An unknown assignee returns an empty list, as spec §14 requires.
- **Same-status no-op** (New→New, InProgress→InProgress) writes nothing and reports nothing. Spec §5 defines it as an
  allowed no-op.
- **Domain/trigger divergence, not a hidden error:** the domain throws BR4 on Completed→Completed (the guard comes
  before the no-op check), while the trigger's `NEW.status IS DISTINCT FROM OLD.status` accepts a same-status update.
  No service path reaches the more permissive trigger branch.
- **Constraint-name string duplicated** in `InitialCreate.cs:154` and `TaskService.cs:90`. If the two drifted apart,
  the failure would be loud (a raw `DbUpdateException`), and the race test would go red. Not silent.
- **No execution strategy or DI registration in `src/`** (`rg "AddDbContext|EnableRetryOnFailure|ChangeTracker|EntityState" src`
  finds nothing). No `RetryLimitExceededException` wrapper sits between `DbUpdateException` and `PostgresException`,
  so the `ex.InnerException is PostgresException` shape holds, as **P1** and **P8** confirm. It also means the context
  lifetime is left entirely to the caller, which is why SF1 and SF2 matter.
- **`Down()` uses `DROP … IF EXISTS`**, as spec §11 prescribes. The design-time factory's hard-coded connection
  string is only a placeholder for model building (spec §13), so it affects no error path.
