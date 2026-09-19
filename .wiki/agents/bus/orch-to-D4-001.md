---
title: "orch → D4: D3 triage and fix record"
type: bus-message
status: sent
updated: 2026-09-19
related: ["[[spec]]", "[[log]]", "[[D3-to-orch-001]]", "[[D3-to-orch-002]]", "[[D3-to-orch-003]]", "[[D3-to-orch-004]]", "[[D3-to-orch-005]]"]
from: orch
to: D4
seq: 1
---

# D3 triage → D4 fixes

Every D3 finding, marked **confirmed** (fixed in D4), **confirmed, docs only**, **deferred to the human**, or
**skipped** (nit, or not required by the brief or spec), with the evidence orch used. D4 was done by orch
(opus): the context transfer to a worker would have cost more than the edits.

## Service: failed save left the entity tracked

| Finding | Disposition | Evidence |
|---|---|---|
| F1 (code-reviewer, major), SF1 (silent-failure, major) | **confirmed, fixed** | New test `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` was red before the fix: "BR3: Employee 10000000-…-0001 is inactive" (a false BR3 naming Alice). Fix: `finally { if (!saved) dbContext.Entry(task).State = EntityState.Detached; }`. Green after. Removing the detach turns only this test red (`d4_red_evidence.txt`). |
| SF2 (silent-failure, major) | **confirmed, fixed** | New test `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds` was red before the fix: "BR4: Task 20000000-…-0001 is Completed" (a false BR4). Same detach pattern in `ChangeTaskStatusAsync`. Green after. Removing the detach turns only this test red. |
| SF3 (silent-failure, minor) | **resolved by the SF1 fix** | Its reproduction needed a stale `Added` task from an earlier failed call, which no longer survives. |
| SF4 (silent-failure, minor), C03 (comments, major) | **confirmed, fixed** | Spec §14 lists `OperationCanceledException`; added to all three methods, plus `DbUpdateException` on `ChangeTaskStatusAsync`. |
| SF5 (silent-failure, nit), J-4 (tests, minor) | **confirmed, fixed** | Doc names the FK case. New test `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException`: a save interceptor deletes the assignee just before the insert; the FK `DbUpdateException` passes through untranslated. Widening the catch filter turns only this test red. |
| F3 (code-reviewer, nit) | **fixed** | Redundant `using System.Threading;` removed (`ImplicitUsings`). |

## BR3 on reassignment

| Finding | Disposition | Evidence |
|---|---|---|
| F2 (code-reviewer, minor), T1 (type-design, minor) | **confirmed, docs only; the schema change is deferred to the human** | The trigger's BR3 branch runs only on `INSERT`, so a raw-SQL `UPDATE … assignee_id` to Carol is accepted (both reviewers' probes). The spec defines creation as the only assignment (§8), so the code matches the approved spec, but ADR 0009 over-claimed "every writer". ADR 0009 wording narrowed. Extending the trigger to `UPDATE OF status, assignee_id` changes the approved schema: that is a question for the human. |

## Tests

| Finding | Disposition | Evidence |
|---|---|---|
| J-1 (major) | **confirmed, fixed** | The ordering test now adds Bob task `…0101` (planned 2026-09-20, due 2026-11-01). The expected `[…0001, …0101, …0003]` differs from ordering by id `[…0001, …0003, …0101]`, by planned start `[…0101, …0001, …0003]` and by insertion. |
| J-2 | **confirmed, fixed** | The filter theory now adds a `New` and a `Cancelled` task for Alice first; dropping the assignee condition would return two tasks. |
| J-3 | **confirmed, fixed** | New `Update_StatusOfCancelledTask_RejectedByBR4Trigger`. Dropping `'Cancelled'` from the trigger's BR4 branch turns only this test red. |
| J-5, C01 (comments, major) | **confirmed, fixed** | New unit test `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` pins the domain guard order; the service-half test's doc now says it goes red only when the service check is removed. |
| J-6 | **confirmed, fixed** | `Create_ValidInput_CopiesTitleIdsAndDates` asserts a non-empty and distinct `Id`. |
| J-11 | **confirmed, fixed** | New `Create_TitleOf200Chars_Succeeds` (the accepted side of the boundary). |
| J-8, C02 (comments, major) | **confirmed, docs only** | The class doc no longer says "exactly": it says constraints and indexes are checked for presence and the trigger is checked as the only user trigger. A superset check stays deliberate, because PostgreSQL 18 lists NOT NULL constraints in `pg_constraint`. |
| J-9 | **evidence limit, recorded** | Removing `IsRowVersion()` fails all 38 integration tests, so the run can't isolate the token. The token's evidence is the two tests that assert `DbUpdateConcurrencyException` directly. |
| J-7, J-10 | **skipped (minor, not required by the spec)** | J-7 would need an employee referenced by one FK only; J-10 needs a timing test for `FOR SHARE`. Listed as next steps. |
| J-12 – J-15 | **skipped (nit)** | |

## Comments

| Finding | Disposition |
|---|---|
| C04 (major) | **confirmed, fixed**: the `ux_employees_email` doc now says the index compares exactly and case-insensitivity comes from `Employee.Create`. |
| C05, C06, C07, C08 | **confirmed, fixed** (docs that claimed more than the code does). |
| C09, C10, C11, C12, C13, C14 | **confirmed, fixed** (missing null-last ordering / empty list, guard order, UTC conversion of inputs, same-status no-op, load-bearing enum names, BR named in a test). |
| C15 – C21 | **skipped (nit)**. |

## Type design

| Finding | Disposition |
|---|---|
| T2, T3 | **skipped (nit)**. T3 (a raw-SQL row with an empty title loads) is outside BR1–BR5, and the brief asks for no CHECK on it. |

## Result

`dotnet build -warnaserror` → 0 warnings, 0 errors; `dotnet test` → 56/56 unit, 38/38 integration.
D4 red evidence: `d4_red_evidence.txt` (scratchpad, quoted in `.wiki/log.md`).

## After the D3 re-run (reports `D3-to-orch-101/102/104/105.md`)

The brief allows D3 one re-run; it ran on `git diff ae7bb74 5b0650c` with the code, comment, silent-failure and test
lenses (type-design was skipped: D4 changed 0 non-doc lines in the domain). Every earlier fix was verified. The
re-run's findings, and orch's corrections to this record:

| Finding | Disposition | Evidence / change |
|---|---|---|
| N1 (code-reviewer, major) = SF6 (silent-failure, major) | **confirmed, fixed (docs)** | Detaching on a failed save (needed for SF2) means the exception's entries are already detached, so EF's "client wins" recovery saves 0 rows. The detach stays; the `DbUpdateConcurrencyException` and `DbUpdateException` docs now say the entries are detached and the retry is calling the method again. |
| SF3 (silent-failure, minor) | **correction: not resolved by SF1** | A caller that leaves its own pending task in the context can still get a BR3 message naming this call's assignee. Documented in `CreateTaskAsync` remarks. Tightening the filter (`ex.Entries.Count == 1 && ReferenceEquals(ex.Entries[0].Entity, task)`) would change spec §14 step 6: for the human. |
| F4 (code-reviewer, nit) | **correction: missing row** | Same issue as J-15 (`DateTimeOffset.UtcNow` in two tests); skipped as a nit. |
| C17 (comments, nit) | **correction** | Marked skipped above, but D4 did fix it (`TaskService` constructor doc). |
| N01 (comments, minor) | **confirmed, fixed** | ADR 0009 now says that covering reassignment needs the trigger on `UPDATE OF status, assignee_id` **and** the function's BR3 lookup on such updates. |
| N02 (comments, minor) | **confirmed, fixed** | The FK test's doc and its spec row no longer claim the constraint-name condition is covered (no other `23514` is reachable through the service). |
| N03, N04, N05, N09 (comments, nit) | **fixed** | "the method" named; "both reject" corrected in the test and service docs; interceptor documented; line wrap. |
| K-1 (tests, minor) | **confirmed, fixed** | The FK test now creates another task on the same context after the untranslated failure; removing the `CreateTaskAsync` detach now fails it too (final D4 evidence: 2 red). |
| K-2 (tests, minor) | **confirmed, fixed** | The F1 test asserts its first failure's inner exception is a `DbUpdateException` (from the trigger). |
| K-4 (tests, minor) | **confirmed, fixed** | README red-evidence section rewritten from a final re-run of all three mutation scripts (FA, FB, D4) on the final code. |
| P1 (code-reviewer, process) | **for the human** | D4 edited the approved spec: §14 (failed-save cleanup steps) and §15 (new test rows), plus the BR4/BR5 pages. The schema sections §10–§12 are unchanged. Listed in the final report for ratification. |
| K-3, N06, N07, N08, SF7, J-3 (optional half), J-9 (narrow variant), J-11 (padded title) | **skipped (nit or optional)** | Listed as next steps. |

Final state: `dotnet build -warnaserror` 0/0; `dotnet test` 56/56 unit, 38/38 integration; FA/FB/D4 mutation runs as
quoted in `.wiki/log.md`.
