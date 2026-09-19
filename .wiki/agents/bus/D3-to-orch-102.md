---
title: "D3 comment-analyzer → orch: re-review of the D4 fixes"
type: bus-message
status: sent
updated: 2026-09-19
related: ["[[spec]]", "[[log]]", "[[orch-to-D3-002]]", "[[orch-to-D4-001]]", "[[D3-to-orch-002]]"]
from: D3-comment-analyzer
to: orch
seq: 102
---

# D3 re-review, lens: comment-analyzer

Scope: `git diff ae7bb74 5b0650c -- src tests docs/adr/0009-br3-br4-enforced-by-trigger.md` (one commit, `5b0650c`;
the working tree is at that commit). I read the diff and every changed file in full at `5b0650c`, plus
`orch-to-D4-001.md`, my `D3-to-orch-002.md`, spec §2a/§3/§5/§8/§11/§14/§15, the trigger SQL in `InitialCreate.cs`
and `d4_red_evidence.txt`. I did not run the build or the tests. "Build and tests green" is orch's claim
(`orch-to-D4-001.md:67`). The red isolation is taken from `d4_red_evidence.txt`. No code was edited.

**Result:** all 14 findings orch marked fixed (C01–C14) are **verified**. The false or missing claims each one
named are gone. C09, C10 and C12 each leave a secondary point from my suggested fix undone. Each residual is a nit
and needs no action to close the finding. **New findings from D4:** critical 0, major 0, minor 2, nit 7.

## 1. Earlier findings marked fixed

| id | verdict | code line | note |
|---|---|---|---|
| C01 | **verified** | `TaskServiceTests.cs:92-97`; new `TaskItemCreateTests.cs:294-312` | The doc now says the test "goes red only if the service check is removed", and that the domain order is pinned by the new unit test. That matches `fb_red_evidence.txt` and spec.md:667. The unit test goes red if BR5 is removed or moved after BR3 (`TaskItem.cs:87` before `:92`). |
| C02 | **verified** | `MigrationAndSeedTests.cs:7-11` | "creates every schema object… (constraints and indexes are checked for presence; the trigger… the only user trigger on `tasks`)" matches `:96-106` and the query at `:93` (`tgrelid = 'tasks'::regclass AND NOT tgisinternal`). |
| C03 | **verified** | `TaskService.cs:56`, `:131-132`, `:180` | `OperationCanceledException` is on all three methods. `DbUpdateException` was added to `ChangeTaskStatusAsync`. Spec §14 (spec.md:558-561) is now satisfied. |
| C04 | **verified** | `EmployeeConfiguration.cs:14-16` | "The index itself compares exactly… Raw SQL can still insert a differently-cased duplicate". Matches `:36` (no collation) and `Employee.cs:54` (`ToLowerInvariant()`). |
| C05 | **verified** | `TaskServiceTests.cs:72-76` | It no longer claims to prove the service check, and it points to the test that does. One wording nit remains: see N04. |
| C06 | **verified** | `TaskServiceTests.cs:304-307` | "the service's query does run, but a context that already tracks Bob keeps its tracked, stale copy". Matches `TaskService.cs:76` (`SingleOrDefaultAsync`) and spec.md:679. |
| C07 | **verified** | `TaskServiceTests.cs:10-15` | "Tests that check what was persisted re-read it through a second context". Every such test does: `:64`, `:88`, `:180`, `:201`, `:329`, `:352`, `:381`, `:414`. |
| C08 | **verified** | `TaskItemCreateTests.cs:262-267` | "earlier… as an instant, although its local clock time (11:00+02:00) reads later than the planned start (10:00+00:00). Proves BR2 compares instants". Matches `:272-273`. The method name still says `…AfterUtcConversion…`, which now disagrees with its doc. The name is pinned by spec.md:606/:689, so any fix belongs in the spec. Not a D4 finding. |
| C09 | **verified** (residual nit) | `TaskService.cs:174-178` | Null-`DueAt`-last and the empty list for an unknown or task-less employee are both stated. Residual: the doc no longer says **why** nulls come last. The code has no null handling (`:198` `OrderBy(t => t.DueAt)`), so the order comes from PostgreSQL's `ASC NULLS LAST` default (spec.md:554-555), and a different provider would change it. Optional: append "(PostgreSQL's `NULLS LAST` default for ascending order)". |
| C10 | **verified** (residual nit) | `TaskItem.cs:62-66`; `TaskService.cs:57-61` | The order of the BR checks is documented in both places, and a reorder now turns a unit test red. Residual 1: "The guards run in that order" leaves out that the title and null checks run first (`TaskItem.cs:74-82`). Residual 2: "the creator's `IsActive` is not checked" (spec.md:176) appears only in the test doc `TaskItemCreateTests.cs:174-175`, not at the guard. |
| C11 | **verified** | `TaskItem.cs:52-53`, `:121` | "Stored as the same instant in UTC (`ToUniversalTime()`)". Matches `:84-85` and `:149`. |
| C12 | **verified** (residual nit) | `TaskItem.cs:124-128` | The no-op case and "even `Completed → Completed` is rejected" match `:136-146` and the §5 table. Residual: the doc does not say that an undefined `newStatus` is rejected **before** BR4 (`:131` runs before `:136`), so a final task given `(TaskItemStatus)99` throws `ArgumentOutOfRangeException`, not BR4. |
| C13 | **verified** | `TaskItemStatus.cs:6-10` | "renaming a member breaks existing rows, the `ck_tasks_status_valid` CHECK and the BR3/BR4 trigger". The trigger compares the literal names at `InitialCreate.cs:156` (`OLD.status IN ('Completed', 'Cancelled')`). "enforced… by the trigger" is also accurate. |
| C14 | **verified** | `TaskItemChangeStatusTests.cs:97-101` | Now names BR1, as the spec §15 map requires (spec.md:688). |

**Triage bookkeeping:** `orch-to-D4-001.md:57` lists C15–C21 as "skipped (nit)", but D4 **did** fix C17: `TaskService.cs:26`
now reads "The clock that supplies `CompletedAt` when a task is completed", which is my suggested text. C15, C16 and
C18–C21 are unchanged. I did not re-check them beyond confirming that.

## 2. New findings introduced by D4

Same severity key as in 002.

| id | sev | conf | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| N01 | minor | 85 | `docs/adr/0009-br3-br4-enforced-by-trigger.md:39-40` | The ADR describes the fix for the reassignment gap as "extending the trigger to `UPDATE OF status, assignee_id`". That change alone does not cover the gap. With the column added, the trigger would fire on an `assignee_id` update, but the function runs the BR3 lookup only for inserts. An update skips it and goes to the BR4 branch. This is the text the human will decide on, so it understates the schema change. | `InitialCreate.cs:148` `IF TG_OP = 'INSERT' THEN` gates the BR3 `SELECT … FOR SHARE` (`:151`). `:156` `ELSIF OLD.status IN (…)` handles every update, BR4 only. | "…covering it would mean extending the trigger to `UPDATE OF status, assignee_id` **and** running the BR3 lookup in `tasks_enforce_br3_br4()` on an update that changes `assignee_id`: a schema change for the human to approve." |
| N02 | minor | 75 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:388`, `:393` | The summary says "a database error other than the BR3 trigger is not translated". The remarks say the test proves the BR3 catch filter "stays narrow". The test covers less than that. It raises SqlState `23503`, and the filter already rejects that on its SqlState condition. If the constraint-name condition were dropped, so that every `23514` CHECK or trigger error became BR3, this test would stay green. | Filter at `TaskService.cs:99-101` has three conditions: `PostgresException`, `SqlState == CheckViolation`, and `ConstraintName == "trg_tasks_br3_assignee_active"`. The test asserts `ForeignKeyViolation` (`:412`). `d4_red_evidence.txt:5-6` tried only "widened to any DbUpdateException". spec.md:682 has the same wording (outside my scope). | Summary: "…that a foreign-key error (23503) raised by the insert is not translated…". Remarks: "Verifies that an error that is not a check violation passes through the BR3 catch filter. The filter's constraint-name condition is not covered here." |
| N03 | nit | 70 | `src/TaskManagement.Application/TaskService.cs:14-15` | The class `<remarks>` say "When a save fails, **the method** detaches…". A class-level doc has no single method, and `ListTasksByAssigneeAsync` never saves. | `:93-115` and `:150-163` are the only detaches. `:181-199` has no save. | "When a save fails, `CreateTaskAsync` and `ChangeTaskStatusAsync` detach the task they added or changed, so…" |
| N04 | nit | 60 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:73-74`; also `src/TaskManagement.Application/TaskService.cs:47-48` | The test doc says "The service's own check and the domain's re-check **both** reject this case". Only the service check runs. It throws before `TaskItem.Create` is called with the same `assignee` instance, so the domain check never runs on this path. The `<exception>` text at `TaskService.cs:47-48` makes the same over-count: "whether caught by this method's own check, the domain's re-check, or the database trigger". The domain re-check cannot fire through this method. The test-doc wording is new in D4: my C05 fix proposed "the service check **or** the domain re-check", and D4 wrote "both". The `TaskService.cs:47` claim predates D4, but D4 rewrote that sentence. I cleared it in round 1 ("correctly lists all three sources"), which was a miss on my part. | `TaskService.cs:82-87` throws on `!assignee.IsActive`. `:89` passes the same instance to `TaskItem.Create`, which tests the same property at `TaskItem.cs:92`. Nothing writes `IsActive` in between. | Test: "Either check alone would reject this case. The service's runs first, so it is the one that fires here…". `TaskService.cs:47`: "…caught by this method's own check or, on a race, by the database trigger (the domain's re-check is a backstop this check pre-empts)." |
| N05 | nit | 55 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:418-432` | `DeleteEmployeeBeforeSaveInterceptor` has no doc. It is the least obvious helper D4 added. It runs a destructive `DELETE` on its own connection every time the context it is attached to starts a save. The reason is given only at the caller (`:390-391`). | Documented sibling: `AssertRejectedAsync` (`DatabaseConstraintTests.cs:285-290`). Counter-precedent: the private helpers `CreateCreator`, `CreateAssignee` (`TaskItemCreateTests.cs:13-15`) and `CreateNewTask` (`TaskItemChangeStatusTests.cs:13`) have none. CS1591 does not apply to private types. Hence nit. | `/// <summary>Deletes <c>employeeId</c> on its own autocommit connection each time the context starts a save, so the insert that follows hits the FK.</summary>` |
| N06 | nit | 55 | `TaskServiceTests.cs:75`, `:96`; `TaskItemCreateTests.cs:297` | These lines carry the C01 fix: each doc names another test, in `<c>` text, as the one that guards its precondition. `<c>` text is not checked by the compiler, so renaming either test silently breaks the pointer. At `:75` the target is in the same class, so a `cref` is possible. `:96` and `:297` cross projects, so no `cref` is possible. | `TaskManagement.IntegrationTests.csproj:19-21` references only the three `src` projects, not the UnitTests project. | `:75` → `<see cref="CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck"/>`. `:96` and `:297`: leave as `<c>`. Optionally add "(spec §15)", since spec.md:597/:667 name both tests. |
| N07 | nit | 60 | `TaskServiceTests.cs:338`, `:362`, `:393`; `docs/adr/0009…:39`, `:57` | Review-process ids appear in XML docs and the ADR: "(D3 findings F1/SF1)", "(D3 finding SF2)", "(D3 finding J-4)", "(D3 findings F2/T1)", "(D4)". Each summary already states the behaviour, so the id tells a future maintainer nothing more. The ids resolve only through `.wiki/agents/bus/`. Nit, not minor, because the project already does this: spec.md:531/:541/:596-597/:680-682 use the same ids, and `TaskServiceTests.cs:305` has "grill Q4". | `git ls-files .wiki/agents/bus` lists `D3-to-orch-00{1..5}.md`, so the ids resolve today. | Drop the ids from the XML docs. Keep them in the ADR if you want the trail. |
| N08 | nit | 70 | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:273` | D4 introduced a third verb for the same kind of remark (C16's topic). The new remark says "**Verifies** BR4 at the database for `Cancelled`". The remark directly above it (`:257`) says "**Enforces** BR4 at the database". Unit tests say "Proves". | `:257` vs `:273`. Spec §2a (spec.md:109): a test "names the BR it proves". | "Proves BR4 at the database for `Cancelled`." (C16 itself was skipped by triage.) |
| N09 | nit | 95 | `tests/TaskManagement.IntegrationTests/TaskServiceTests.cs:307` | Reflow leftover: the line stops after "The service must translate the" (about 50 characters), while the lines around it run to about 118. The rendered doc is unaffected. | `:305-309` as committed. | Reflow `:305-309`. |

## 3. Checked and clean

- **The two detach comments** (`TaskService.cs:112`, `:160`) say exactly why each `finally` exists: an `Added` task
  is re-sent on the next save, and a `Modified` task becomes the starting state of a retry.
  `CreateTaskAsync` remarks (`:57-61`), `ChangeTaskStatusAsync` remarks (`:133-136`, "a retry… reloads the current
  row") and the new `<exception>` texts (`:45`, `:52-56`, `:131-132`) match the code. The FK example in `:53-54` is
  proven by `CreateTaskAsync_AssigneeDeletedBeforeInsert_…`.
- **Ordering test doc** (`TaskServiceTests.cs:218-221`): I checked all three "differs from" claims against the seed
  (spec.md:458-460) and the inserted row (`:229-232`). Expected `[…0001, …0101, …0003]`. By id: `[…0001, …0003,
  …0101]`. By planned start, nulls last: `[…0101, …0001, …0003]`. By insertion: `[…0001, …0003, …0101]`. All three
  differ, and the dates in the doc match the SQL.
- **Filter theory doc** (`:263-266`): with the assignee condition dropped, `New` returns `…0001` + `…0102`, and
  `Cancelled` returns `…0003` + `…0103`. Two tasks in each case, as the doc says.
- **New failed-save test docs** (`:333-337`, `:357-361`): the failure each describes (an `Added` task re-sent; the
  tracked unsaved `Completed` causing a false BR4) matches the code path and orch's pre-fix red messages.
- **`TaskItemStatus` remarks**, the **`TaskItem.ChangeStatus` remarks** and the **new unit-test docs**
  (`TaskItemCreateTests.cs:32-36`, `:100`, `:157`, `:281-300`) match their code. Every `cref` resolves.
- **ADR 0009 narrowing** (`:37-43`): "BR3 **on insert**" matches the trigger (`InitialCreate.cs:148`, `:168`). "creation
  is the only moment the model sets an assignee" matches spec §8 (spec.md:283-284). The raw-SQL `UPDATE` gap is
  stated correctly. The only problem is the proposed remedy (N01). The other src comments already say "on insert"
  (`InitialCreate.cs:28`), so no sibling comment still over-claims.
- **Not flagged on purpose:** "canceled" in the three `OperationCanceledException` docs next to "cancelled" in
  `TaskItemStatus.cs:22` (no spelling rule in AGENTS.md or the spec); the literal `§` at `TaskItemCreateTests.cs:297`
  where other docs use `&#167;` (both valid).
