---
title: "D3 → orch: pr-test-analyzer review"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[orch-to-D3-001]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]"]
from: D3-pr-test-analyzer
to: orch
seq: 5
---

# D3 review 5: pr-test-analyzer

Scope: `tests/TaskManagement.UnitTests`, `tests/TaskManagement.IntegrationTests`, against spec §15 (`.wiki/domain/spec.md`
lines 559–686) and the red evidence in the scratchpad `d3\` folder (`fa_red_evidence.txt`, `fb_red_evidence.txt`,
`test.txt`, `build.txt`). No worker bus reports read. No code edited, no ablations re-run.

Severity mapping (agent rating 1–10 → bus severity): 9–10 critical, 7–8 major, 4–6 minor, 1–3 nit.

## Headline

All five BRs have a test that goes red when their enforcement is removed, at both the domain layer and the DB layer
(matrix below). BR3's two service halves are red-proven too. Every spec §15 test exists under its exact name, and every
test method has its trait. The gaps are in **use case 3's list assertions** (seed data too thin to tell apart the
orderings and filters a regression could produce), **half of the BR4 trigger** (Cancelled), and **evidence
completeness** (xmin was never removed). **0 critical, 1 major, 10 minor, 4 nit.**

## BR × layer red-evidence matrix

| BR | Domain (FA ablation → red) | DB (FB ablation → red) | Service |
|---|---|---|---|
| BR1 | `CompletedAt` assignment removed → 4 tests (`fa:11-15`) | CHECK dropped → 3 behavioural + schema (`fb:1-5`) | no own enforcement; not ablated |
| BR2 | guard removed → 2 tests (`fa:5-7`) | CHECK dropped → 1 behavioural + schema (`fb:6-8`) | no own enforcement; not ablated |
| BR3 | guard removed → 1 test (`fa:3-4`) | trigger not created → `Insert_TaskForInactiveAssignee_…` + race test (`fb:12-16`) | service check removed → Carol/Carol test (`fb:17-18`); translation removed → race test (`fb:19-20`) |
| BR4 | guard removed → 9 (8 disallowed table rows + `FromCompleted`) (`fa:8-10`) | trigger not created → `Update_StatusOfCompletedTask_…` (`fb:12-16`); **xmin token: not ablated** (M-9) | none needed (spec §14 lines 556–557) |
| BR5 | guard removed → 1 test (`fa:1-2`) | CHECK dropped → 1 behavioural + schema (`fb:9-11`) | no own enforcement; not ablated |

The FA runs cover the unit project only (`fa:1` "1 failed, 53" = 54 = the unit total in `test.txt:70`). That is how the
run was scoped, not a gap.

## Findings

| id | sev | conf | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| J-1 | **major** (7) | 90 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:216-223` (expected order `:21-25`) | The ordering test cannot tell "ordered by `DueAt`" apart from other orderings a regression could produce. Bob has 2 seeded tasks: `…0001` (due 2026-10-10, planned 2026-10-01) and `…0003` (due null, planned null). Three plausible regressions still produce `[…0001, …0003]` and pass: `OrderBy(t => t.Id)` (`2000…0001 < 2000…0003`), `OrderBy(t => t.PlannedStartAt)` (confusing deadline with planned start; NULLS LAST keeps the same order), and dropping `OrderBy` entirely (`[uncertain]`: a one-page seq scan returns heap order, which is insert order `0001, 0003`, migration `:114-117`). The user-visible failure it would miss: use case 3 "ordered by deadline" (spec line 22) returns tasks in the wrong order. | `TaskService.cs:159` `OrderBy(t => t.DueAt).ThenBy(t => t.Id)`; the test asserts only `Assert.Equal(BobsTaskIdsOrderedByDueAt, …)` over those two rows; seed spec §12 lines 458–460 | In Arrange, create one more Bob task through the service with `dueAt` 2026-11-01 and `plannedStartAt` 2026-09-20. Expect `[…0001, new, …0003]`. Its v7 id (it starts `01…`, the 2026 ms timestamp) sorts before `2000…`, its planned date sorts first and it is inserted last, so the Id, PlannedStartAt and heap-order regressions all go red. |
| J-2 | minor (6) | 95 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:255-269` | The status-filter theory does not prove that the filter is combined with the assignee filter. In the whole seed, `New` matches only `…0001` and `Cancelled` matches only `…0003`. A regression that rebuilds the query from `dbContext.Tasks` when `status` is set, and so drops `AssigneeId == assigneeId`, still returns exactly one row with the expected id. The user would then see other employees' tasks. | `TaskService.cs:152-157`; seed §12: `…0001` New, `…0002` Completed, `…0003` Cancelled | In Arrange, create a `New` task for Alice (creator Bob) through the service. `Bob`/`New` must still return only `…0001`. |
| J-3 | minor (6) | 95 | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:258-267` | The BR4 trigger is tested on one path only: `Completed → New`. Nothing tests (a) its **Cancelled** branch or (b) what it allows. For (a): a trigger that checks only `'Completed'`, or has the `'Canceled'` typo (spec §2/§3.3 flags this spelling trap), passes the whole suite, so raw-SQL writers could reopen Cancelled tasks. The brief names both states: "BR4 `Completed` and `Cancelled` are final" (`ORCHESTRATOR_PROMPT.md:26`). For (b): drop `NEW.status IS DISTINCT FROM OLD.status` and a same-status write to a final row starts failing, and no test notices (spec lines 428–429 record that the container accepted it). Rated minor, not major: no application path reaches the trigger, because the domain throws BR4 first and the xmin token fires before the trigger (spec §14 lines 556–557). | migration `20260918164447_InitialCreate.cs:156` `ELSIF OLD.status IN ('Completed', 'Cancelled') AND NEW.status IS DISTINCT FROM OLD.status`; `fb:12-16` shows only the Completed row going red | Add `Update_StatusOfCancelledTask_RejectedByBR4Trigger`: `UPDATE tasks SET status = 'New' WHERE id = '20000000-0000-0000-0000-000000000003'` → `23514` / `trg_tasks_br4_final_status`. It discriminates because `New` + null `completed_at` passes the BR1 CHECK. Optionally also add `Update_SameStatusOnCompletedTask_Accepted` (`SET status = 'Completed'` on `…0002` → 1 row). |
| J-4 | minor (5) | 95 | `src/TaskManagement.Application/TaskService.cs:88-96` (no test) | Nothing sends a non-BR3 `DbUpdateException` through `CreateTaskAsync`, so the spec's rule that "every other `DbUpdateException` propagates unchanged" (spec §14 step 6, line 527) is untested. Broaden the filter to `catch (DbUpdateException ex)`, or drop the `ConstraintName` condition, and every test stays green. BR2 and BR5 cannot reach the DB through the service because the domain rejects them first. The user would then get "employee is inactive" for an unrelated DB failure. | the only failing insert in `TaskServiceTests` is the BR3 race (`:291-308`) | Add a test with a `SaveChangesInterceptor` (`SavingChangesAsync`, EF 10.0.12 XML) on the test's own options. Its setup: a new active employee inserted by raw SQL. The interceptor deletes that employee over a separate connection just before the insert. The insert then fails with FK `23503`, because the trigger's `SELECT` finds no row, leaves NULL and does not raise BR3. Assert `ThrowsAsync<DbUpdateException>` (exact type) with inner `ConstraintName == "fk_tasks_employees_assignee_id"`. |
| J-5 | minor (5) | 90 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:85-101`; `src/TaskManagement.Domain/TaskItem.cs:86,91` | This is the **only** test that proves the service's BR3 check exists (`fb:17-18`), and its power depends on BR5 (`:86`) being checked before BR3 (`:91`). No unit test pins that order. If the two guards are swapped, every test still passes, and from then on the service check can be removed without any red. The test's XML doc says it "goes red if either check is removed **or reordered**" (`:87-88`). That is false for a reorder: the service throws BR3 before the domain runs. Spec §3.2 (line 166) says "pin it anyway". | spec line 659 names the dependency; `TaskItemCreateTests.cs:136-165` use an active employee for BR5 and distinct employees for BR3, so neither is sensitive to the order | Add the unit test `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5`: deactivate one `Employee`, pass it as both creator and assignee, assert `RuleId == "BR5"`. A reorder then goes red. Fix the doc wording. |
| J-6 | minor (5) | 80 | `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs:36-51` | No test asserts that `TaskItem.Id` is set or that ids are distinct. `EmployeeTests` does both for `Employee` (`EmployeeTests.cs:23`, `:155`). Every integration test creates at most one task per fresh database, so a regression that leaves `Id = Guid.Empty` passes. `CreateTaskAsync_ValidInput_PersistsNewTask` re-reads by `created.Id`, which finds the `Guid.Empty` row. The user-visible failure: every task after the first fails with a `pk_tasks` violation. `[uncertain]` whether EF 10 rejects a CLR-default key on `Add` when `ValueGeneratedNever()` is set (`TaskItemConfiguration.cs:54`). I believe it does not. | `grep Guid.Empty tests` → only `EmployeeTests.cs:23`; `TaskItem.cs:105` `Id = Guid.CreateVersion7()` | Add `Assert.NotEqual(Guid.Empty, task.Id)` to `Create_ValidInput_CopiesTitleIdsAndDates`, plus a two-call distinct-id test that mirrors `EmployeeTests.cs:150-156`. |
| J-7 | minor (4) | 85 | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:184-194` | Delete-Restrict cannot catch **one** FK being switched to Cascade. Bob, the deleted employee, is both a creator (`…0002`) and an assignee (`…0001`, `…0003`). If one FK cascades, the other Restrict FK still finds rows that reference Bob, so the delete still fails. The test accepts either constraint name. The schema test checks FK names only, not their delete rule. Spec §10 (lines 346–347): Cascade "would silently delete task history". | seed §12: Alice and Bob each appear as creator and as assignee, and Carol has no tasks; `MigrationAndSeedTests.cs:18-19` checks names only | Extend the schema test: `SELECT conname AS "Value" FROM pg_constraint WHERE contype = 'f' AND confdeltype = 'r'` must equal both FK names. |
| J-8 | minor (4) | 95 | `tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs:95-103` | Constraints and indexes are checked with `Assert.Contains` (superset). Spec §11 says the migration "must contain exactly these, no more" (line 352) and names the risk of a stray default `IX_tasks_assignee_id`. Such an index, or any extra CHECK, passes. Triggers are checked exactly (`:105`). | `foreach … Assert.Contains(expected, constraintNames)` / `indexNames` | Use `Assert.Equal` on the sorted sets. `pg_indexes` also lists `pk_employees` and `pk_tasks`, so add them to the expected index names. `[uncertain]`: on PostgreSQL 18+, NOT NULL constraints appear in `pg_constraint`, so filter `contype IN ('p','f','c','u')` to stay image-proof. |
| J-9 | minor (4) | 95 | `fb_red_evidence.txt:1-21`; `src/TaskManagement.Infrastructure/Configurations/TaskItemConfiguration.cs:62` | The spec's BR→test mapping credits `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` as the BR4 DB test for the xmin token, but no ablation removed `IsRowVersion()`. By reasoning it would go red: without the token, context 2's `UPDATE` reaches the BR4 trigger, which raises a `DbUpdateException`, and exact-type `Assert.ThrowsAsync<DbUpdateConcurrencyException>` rejects that. `fb:12-16` shows the test stays green when the trigger alone is dropped, so it isolates xmin. This is an evidence gap, not a coverage gap. | the six FB ablations listed; `DatabaseConstraintTests.cs:204-217` | Run one more ablation that removes the token but leaves the schema alone, so the migration and snapshot stay valid, e.g. `IsRowVersion().IsConcurrencyToken(false)` (`[uncertain]` whether EF's pending-model-changes check ignores the concurrency flag; removing `:62` outright changes the model and would break every integration test, not just this one). Run `--filter ConcurrentStatusChange` and append the result to the evidence. |
| J-10 | minor (4) | 85 | migration `:151` (no test) | Nothing tests the `FOR SHARE` in the BR3 trigger (spec §8 line 294–295: "a concurrent deactivation of that employee waits for it"). The race test commits the deactivation **before** the insert, and a plain `SELECT` would catch that too. Remove `FOR SHARE` and every test stays green. | race test `TaskServiceTests.cs:293-295` deactivates first, then inserts | Use two `NpgsqlConnection`s. Connection 1: `BEGIN; INSERT` a task for Bob and keep the transaction open. Connection 2: `SET lock_timeout = '500ms'; UPDATE employees SET is_active = false WHERE id = Bob` → `PostgresException` SqlState `"55P03"`. Without `FOR SHARE`, the FK's `FOR KEY SHARE` does not conflict with a non-key `UPDATE`, so the update succeeds and the test goes red. |
| J-11 | minor (4) | 95 | `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs:97-105` | The title length guard has only its reject side (201). Two regressions reject valid titles and pass: `>=` (a 200-char title rejected) and measuring the untrimmed `title.Length` (a padded valid title rejected). `EmployeeTests` has both 200 and 201 (`:69-89`). | `TaskItem.cs:75` `trimmedTitle.Length > TitleMaxLength` | Add `Create_TitleOf200Chars_Succeeds`. Optionally add a padded variant: `"  " + new string('a', 200) + "  "` succeeds. |
| J-12 | nit (2) | 90 | `tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs:64-73` | Nearly a tautology: `PostgresFixture.cs:39` already ran `MigrateAsync`, so `pending` is empty and `applied == all` by construction. The test can fail only if setup already threw. Rated nit, not minor: `[uncertain]` EF 9+ makes `Migrate` throw on `PendingModelChangesWarning` by default (from memory; the 10.0.12 XML does not say so). If it does, the fixture already catches model/migration drift. | fixture `CreateDatabaseAsync` → `MigrateAsync` | Add `Assert.False(_dbContext.Database.HasPendingModelChanges())` (present in Relational 10.0.12 XML). |
| J-13 | nit (2) | 90 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:196` | `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged` asserts only `Assert.NotNull(persisted.CompletedAt)`, not the seeded value `2026-09-04T15:30:00Z`, so "unchanged" is only half checked. | seed §12 line 459 | `Assert.Equal(new DateTimeOffset(2026, 9, 4, 15, 30, 0, TimeSpan.Zero), persisted.CompletedAt)`. |
| J-14 | nit (2) | 85 | `tests/TaskManagement.UnitTests/EmployeeTests.cs:14-24` | `FullName` trimming (spec §3.1 line 133) is untested: every test passes a name that is already trimmed. Drop `.Trim()` from `FullName` (`Employee.cs:47,63`) and everything stays green. | no padded full-name input in `EmployeeTests.cs` | Add `Create_FullNameWithSurroundingSpaces_StoresTrimmed`. |
| J-15 | nit (1) | 95 | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:212,215` | Uses `DateTimeOffset.UtcNow`, where spec §6 (lines 269–270) asks B3 for fixed, whole-second times. Harmless today, because the value is never read back. | `firstTask.ChangeStatus(TaskItemStatus.Completed, DateTimeOffset.UtcNow)` | Use a fixed `new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero)`. |

Keep the J-1 and J-2 Arrange steps in their own tests. The extra Bob task from J-1 is `New`, so if it were shared,
`Bob`/`New` in J-2 would return two rows. A side benefit of J-1: the create path then round-trips non-null dates
through EF, which no integration test does today (`CreateTaskAsync_ValidInput_PersistsNewTask` passes null dates,
`TaskServiceTests.cs:59`).

## Checked and clean

- **§15 completeness**: I diffed the spec §15 test names against the test methods (`comm` over the backticked
  `A_B_C` names in spec lines 559–672 vs `public (async Task|void) X` in `tests/**/*Tests.cs`). All 67 names match
  exactly. Counts reconcile: 54 unit (`test.txt:70`) and 34 integration (`test.txt:130`).
- **Traits**: `[Fact]`/`[Theory]` count equals the `[Trait("Category", …)]` count in all six files (13/13, 4/4, 16/16,
  13/13, 5/5, 16/16).
- **Assertion style**: tests assert `RuleId`, never message text (spec §3.4). Exception checks use exact-type
  `Assert.Throws`/`ThrowsAsync`, which is what lets the xmin and race tests tell `DbUpdateConcurrencyException`,
  `DbUpdateException` and `BusinessRuleViolationException` apart.
- **Transition table**: all 16 §5 rows, with `changedAt = T0 + 1h`, which differs from the `T0` used to reach `from`.
  An overwritten `CompletedAt` on a disallowed row is therefore caught (`TaskItemChangeStatusTests.cs:58,76`).
- **BR2 boundaries** at both layers: equal accepted (`TaskItemCreateTests.cs:206`, `DatabaseConstraintTests.cs:117`),
  all three null permutations, and the instant-vs-wall-clock case (`:264`).
- **BR1 at the DB, both directions** (Completed without `completed_at`; New with it) plus the update path.
- **Race test** asserts the full chain (`BusinessRuleViolationException` → `DbUpdateException` → `PostgresException`
  with the trigger name) and that no row persisted (`TaskServiceTests.cs:301-307`).
- **`Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`** is a real negative test: a trigger written as
  `IS NOT TRUE` would mask the FK and turn it red.
- **Independence**: one container, a fresh migrated database per test (`PostgresFixture.cs:30-41`). No in-memory
  provider.
- **`CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`** not going red when the service check is removed
  (`fb:17-18`) is by design: the domain re-checks, and the Carol/Carol test exists for the service half (but see J-5).
- **Spec `[uncertain]`s settled green by passing tests**: `ORDER BY due_at` puts nulls last (§14 lines 550–551;
  `test.txt:96`). `ExecuteSqlRawAsync` surfaces an unwrapped `PostgresException` (§15 lines 623–624; every
  `AssertRejectedAsync` test passed). EF returns the stale tracked employee (§15 line 671; `test.txt:99`). CA1001 on the
  fixture (§15 lines 618–619) gives 0 warnings under `-warnaserror` (`build.txt:10`).
- Not raised on purpose (none are in §15 and the task caps unrequired items at nit): CancellationToken tests, an
  untracked-result assertion for `ListTasksByAssigneeAsync`, and a case-insensitive duplicate e-mail at the DB (ADR 0005
  puts that in the domain).
