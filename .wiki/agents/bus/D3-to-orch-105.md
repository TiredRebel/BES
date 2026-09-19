---
title: "D3 → orch: pr-test-analyzer re-review of the D4 fixes"
type: bus-message
status: sent
updated: 2026-09-19
related: ["[[spec]]", "[[orch-to-D3-002]]", "[[orch-to-D4-001]]", "[[D3-to-orch-005]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]"]
from: D3-pr-test-analyzer
to: orch
seq: 105
---

# D3 review 105: pr-test-analyzer, re-run on D4

Scope: `git diff ae7bb74 5b0650c -- src tests docs/adr/0009-br3-br4-enforced-by-trigger.md`, checked against
`orch-to-D4-001.md`, my `D3-to-orch-005.md`, spec §14–§15 and `d4_red_evidence.txt` (plus `mutate_d4.py`, the
script that produced it). The working tree matches `5b0650c` for `src` and `tests`. No code edited, no tests run, no
ablations run: every "by reasoning" below is my own trace, not a run.

Severity mapping as before (agent rating 1–10 → bus severity): 9–10 critical, 7–8 major, 4–6 minor, 1–3 nit.

## Headline

Of the findings orch marked fixed, J-1, J-2, J-4, J-5, J-6 and J-11 are **verified**. For J-3, the fix it asked for
(test the trigger's `Cancelled` branch) is **verified**, and a test shows it red. The part I had marked "optionally"
(nothing tests what the trigger *allows*) is still open. Only four D4 changes were ablated (`d4_red_evidence.txt:1-8`). J-1, J-2, J-5, J-6 and J-11 are
verified by reasoning only.

New findings from D4: **0 critical, 0 major, 3 minor, 1 nit.** Taken together: D4's new detach code is tested on one
failure path per method only, and one of its two tests does not check that it reached that path. Separately, the
README's red-evidence claims were not updated for the new tests.

## 1. Earlier findings: verification

| id | orch disposition | verdict | code | evidence |
|---|---|---|---|---|
| J-1 | fixed | **verified** (by reasoning) | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:23-28` (expected `[…0001, …0101, …0003]`), `:229-236` (raw-SQL insert of `30000000-…-0101`, planned 2026-09-20, due 2026-11-01) | Traced against the seed (`20260918164447_InitialCreate.cs:115-117`). Order by Id gives `[0001, 0003, 0101]` (`3000…` sorts after `2000…`). Order by PlannedStartAt gives `[0101, 0001, 0003]`. Heap/insert order gives `[0001, 0003, 0101]` (`[uncertain]` as before: it assumes a seq scan). Order by Title gives `[0003, 0101, 0001]`. DESC gives `[0003, 0101, 0001]` (PG puts NULLs first). Each differs from the expected order. Residual, not a D4 regression: D4 inserts the row by raw SQL, not through `CreateTaskAsync`. The side benefit I named, a non-null `PlannedStartAt`/`DueAt` round-trip through the EF create path, was therefore not obtained. `CreateTaskAsync_ValidInput_PersistsNewTask` (`:62`) still passes null dates. |
| J-2 | fixed | **verified** (by reasoning) | `TaskServiceTests.cs:280-290` | Alice gets `…0102` `New` and `…0103` `Cancelled`. If the assignee condition (`TaskService.cs:191`) were dropped, `New` would return `0001` + `0102` and `Cancelled` would return `0003` + `0103`, so `Assert.Single` (`:289`) fails in both theory rows. The J-1 and J-2 arrangements stay in separate tests, as asked. |
| J-3 | fixed | **fix verified; optional half open** | (a) `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:276-283`; (b) no test | (a) **Verified**: `UPDATE … SET status = 'New'` on `…0003` → `23514` / `trg_tasks_br4_final_status`. It is red-proven: dropping `'Cancelled'` from the `IN` list turns only this test red (`d4_red_evidence.txt:7-8`, mutation in `mutate_d4.py`). The `'Canceled'` typo is caught the same way. (b) **Not addressed**: J-3 also said nothing tests what the trigger allows. Drop `AND NEW.status IS DISTINCT FROM OLD.status` (migration `:156`) and a same-status write to a final row starts failing, yet every test stays green. Evidence: the only `UPDATE`s in `DatabaseConstraintTests` are at `:85` (does not touch `status`, so the trigger does not fire), `:263` and `:279` (both change the status). `mutate_d4.py` never mutates that clause. In my J-3 fix this part was marked "optionally", so I rate the gap as a residual, not a failed fix. The fix still stands: `Update_SameStatusOnCompletedTask_Accepted`, `UPDATE tasks SET status = 'Completed' WHERE id = '…0002'`, expect 1 row. `…0002` has `completed_at` set, so the BR1 CHECK passes. |
| J-4 | fixed (filed in orch's Service table, with SF5) | **verified** | test `TaskServiceTests.cs:396-432`; filter `src/TaskManagement.Application/TaskService.cs:99-101` unchanged | Exact-type `Assert.ThrowsAsync<DbUpdateException>` (`:408`), inner `23503` / `fk_tasks_employees_assignee_id` (`:411-413`), nothing persisted (`:415`). Red-proven: widening the filter to `catch (DbUpdateException ex)` turns only this test red (`d4_red_evidence.txt:5-6`). The interleaving is real: the interceptor deletes on its own `NpgsqlConnection` (`:425-429`). See K-1 for what this test does *not* cover. |
| J-5 (+ C01) | fixed | **verified** (by reasoning) | unit test `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs:303-313`; guard order `src/TaskManagement.Domain/TaskItem.cs:87` (BR5) before `:92` (BR3); doc `TaskServiceTests.cs:92-97` | One deactivated employee as both creator and assignee → asserts `RuleId == "BR5"`. Swap the two guards and the test gets BR3 → red. The false "or reordered" claim is gone from the service test's doc. No unit ablation was run (the D4 mutations are integration-only). |
| J-6 | fixed | **verified** (by reasoning) | `TaskItemCreateTests.cs:47-48` | `Assert.NotEqual(Guid.Empty, task.Id)` catches a missing assignment. The second `Create(...).Id` comparison catches a constant id. Neither is tautological. |
| J-11 | fixed | **verified** for the `>=` regression (by reasoning) | `TaskItemCreateTests.cs:287-292` | A 200-character title must succeed, and the test asserts `Title.Length == 200`, so `>=` goes red. Residual: the optional padded variant was not added. Measuring `title.Length` instead of `trimmedTitle.Length` (`TaskItem.cs:76`) therefore still passes every test. The only padded-title test (`:62-64`) uses a short title. |
| J-8 | docs only | **doc verified; gap stands** | `tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs:7-11` vs `:98`, `:103` (`Assert.Contains`), `:106` (`Assert.Equal`) | The class doc now matches the code. Orch's reason for keeping a superset check (PostgreSQL 18 lists NOT NULL constraints in `pg_constraint`) applies to constraints only, not to `pg_indexes`. The fixture also pins `postgres:17-alpine` (`PostgresFixture.cs:15`). An exact index-name check would not be affected by the image, so a stray `IX_tasks_assignee_id` (spec §11) still passes. I leave the disposition to orch. |
| J-9 | evidence limit, recorded | **not fixed** (evidence gap unchanged) | `mutate_d4.py` has no xmin mutation | Orch's note covers only removing `IsRowVersion()` outright. J-9 had already predicted that this breaks every integration test. The narrow variant I proposed (`IsConcurrencyToken(false)`, keeping the model) was not tried. `[uncertain]` whether that variant would isolate the token. |
| J-7, J-10, J-12–J-15 | skipped | not re-checked | none | Deliberately deferred or skipped by orch. Not re-litigated. |

## 2. New findings introduced by D4

| id | sev | conf | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| K-1 | minor (5) | 85 | `src/TaskManagement.Application/TaskService.cs:108-115`; `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:396-416` | Spec §14 step 7 says the failed-save detach happens on **any exception, "translated or not"** (spec lines 531–533). Only the *translated* path is tested. The FK test (J-4) builds its own context and never uses it again after the failure. So a refactor that moves the detach into the BR3 `catch` block, and drops `saved`/`finally`, passes every test. User-visible failure: after an FK error, the caller's next save on the same context sends the orphan insert again and fails again. After a transient error, the next save persists the task the caller was told had failed, which gives a duplicate if the caller retried it. `ChangeTaskStatusAsync` has the same shape: only `DbUpdateConcurrencyException` is tested (`:375-376`), so narrowing its detach to that type passes too. That case is lower value, because no other `DbUpdateException` is reachable there today. | `d4_red_evidence.txt:1-2`: removing the create detach turns **only** `CreateTaskAsync_AfterTriggerRejection_…` red, and the J-4 test stays green, so it does not observe the detach. The BR3-catch-only variant is reasoned, not run. | At the end of `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException`, add `var next = await service.CreateTaskAsync("Next", AliceId, BobId, null, null);` and assert that `readContext` finds `next.Id`. The interceptor's second `DELETE` hits 0 rows, which is harmless. If the orphan is still `Added`, the batch fails on the FK → red. |
| K-2 | minor (4) | 85 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:347-348` | `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` asserts only that the first call throws `BusinessRuleViolationException`. It does not check that the exception came from the save (the trigger). Its setup relies on EF handing back the stale tracked Bob (spec §15 recorded this as `[uncertain]` until it ran). Suppose the service's employee lookup stops returning the tracked instance, e.g. `.AsNoTracking()` on the reads or a reload. Then the service's own BR3 check (`TaskService.cs:82-87`) throws before `Add`, nothing is left to detach, and this test passes with or without the detach. The race test would go red in that case (`:325-327` asserts the inner chain), but repairing it would not reveal that this test had also lost its power. | `:347-348` versus the race test's `:324-327`, which pins the path | Capture the exception and add `Assert.IsType<DbUpdateException>(ex.InnerException);`, one line. The SF2 and J-4 tests already pin their paths with exact-type asserts (`:375`, `:408`). |
| K-3 | nit (3) | 80 | `TaskService.cs:93-97`, `:150-154` (the `saved` flag) | The ablation set tests one direction only. `mutate_d4.py` checks that the detach is **never** skipped on failure (`if (false && !saved)`). Nothing checks that it is skipped on **success**. If the detach becomes unconditional, every test stays green: all success-path tests read the row back through a second context (`:64-65`, `:180-183`, `:352-354`, `:381-384`) or assert on the returned object. Effect: after a successful create or status change, the returned task is no longer tracked, so a caller that relies on identity resolution on that context gets a second instance. Impact is low, and asserting `EntityState` couples the test to EF tracking, hence nit. | by reasoning; no success-path test inspects `dbContext.Entry(...)` | In `CreateTaskAsync_ValidInput_PersistsNewTask`, add `Assert.Equal(EntityState.Unchanged, _dbContext!.Entry(created).State);`. |
| K-4 | minor (4) | 85 | `README.md:87-89`, `:91` (outside orch's pathspec; D4 edited `:88-89` in `5b0650c`) | The README's red-evidence column is now wrong for the tests D4 added. **BR5** (`:89`): it lists the new `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` but still says "remove BR5 guard → 1 test red". It is now 2: without the guard, the new test gets BR3. **BR4** (`:88`): it lists the Cancelled test, but "trigger not created →" still names only the Completed test and the schema test. **BR3** (`:87`, not edited by D4): "remove trigger-error translation → only the race test red" and "trigger not created → …" are both now incomplete. In both ablations, `CreateTaskAsync_AfterTriggerRejection_…` also goes red: its first `ThrowsAsync<BusinessRuleViolationException>` gets a `DbUpdateException`, or no exception at all. `:91` says every listed test "was run red … (confirmed in fan-in A and fan-in B log entries)". That is false for the two D4 tests: the Cancelled test was run red in D4, not in fan-in A or B, and the BR5 guard-order test has no red run anywhere. | the counts are by reasoning; `d4_red_evidence.txt` has integration mutations only; `git diff ae7bb74 5b0650c -- README.md` | Correct the three evidence cells. Change `:91` to cite the D4 evidence as well. Run one unit ablation (swap the BR5/BR3 guards in `TaskItem.Create`) and append the result to `d4_red_evidence.txt`. |

## Checked and clean

- **Tautology / brittleness of the new tests**: each new test asserts behaviour, not implementation (it re-reads
  through a second context), except the one line K-3 proposes. The SF2 test pins its precondition with an exact-type
  `DbUpdateConcurrencyException` and a fixed `CompletedAt` (`TaskServiceTests.cs:375`, `:384`). Without the detach it
  goes red with a false BR4, as orch reported (`d4_red_evidence.txt:3-4`). The raw-SQL Arrange rows (J-1, J-2) satisfy
  every CHECK: BR1 (`New`/`Cancelled` with null `completed_at`), BR2 (09-20 ≤ 11-01) and BR5 (creator ≠ assignee).
- **Counts and traits**: 56 unit = 54 + 2 (`Create_TitleOf200Chars_Succeeds`, the BR5 guard-order test). 38 integration
  = 34 + 4 (Cancelled trigger, F1, SF2, J-4). Each new method carries its `[Trait("Category", …)]`.
- **Spec §15**: the new unit tests have rows at spec lines 596–597, the new integration tests at 657 and 680–682, and
  the BR5 guard-order test is also in the BR→test mapping (line 692).
- **Spec §15 "Failing-then-passing" paragraph** (after the BR→test mapping): it states the ablation relationship in
  general terms ("disabling a domain guard turns its A2 row red"). That still holds for the new BR5 row by reasoning,
  so unlike README `:91` it is not a false claim of a run. Not a finding.
- **Interceptor options** (`TaskServiceTests.cs:401-404`) match `PostgresFixture.CreateContext`
  (`UseNpgsql(connectionString)` only), plus the interceptor.
