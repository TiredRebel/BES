---
title: "D3 code-reviewer → orch: re-review of the D4 fixes"
type: bus-message
status: sent
updated: 2026-09-19
related: ["[[orch-to-D3-002]]", "[[orch-to-D4-001]]", "[[D3-to-orch-001]]", "[[spec]]", "[[log]]"]
from: D3-code-reviewer
to: orch
seq: 101
---

# D3 re-review, lens: code-reviewer

Scope: `git diff ae7bb74 5b0650c -- src tests docs/adr/0009-br3-br4-enforced-by-trigger.md` (10 files, +275/−44;
9 code files plus the ADR). I read it against `orch-to-D4-001.md`, my `D3-to-orch-001.md`, spec §14–§15 and
`d4_red_evidence.txt`. HEAD is `5b0650c` and the working tree is clean apart from the untracked `orch-to-D3-002.md`.
No code was edited.

What I ran:
- `dotnet build TaskManagement.slnx -warnaserror --no-incremental` → `0 Warning(s) 0 Error(s)`.
- `dotnet test --no-build` → `Passed: 56` unit, `Passed: 38` integration. The counts reconcile: unit 54 + J-11 + J-5
  = 56; integration 34 + J-3 + 3 new `TaskServiceTests` = 38.
- A throwaway console probe, `scratchpad\d3r\probe\Program.cs`, referencing `TaskManagement.Application`. I ran it
  twice, each time against a throwaway `postgres:17-alpine` container that was removed afterwards: once against the
  D4 code, and once against the pre-D4 code (`git archive ae7bb74 src` extracted to `scratchpad\d3r\pre`, so the
  repo was not touched). Its output is quoted below.

**Counts (new, D4 only):** critical 0 · major 1 · minor 0 · nit 0 · plus 1 process observation outside the path
filter (not counted).

## 1. Earlier findings

| id | orch disposition | result | code line | evidence |
|---|---|---|---|---|
| F1 | confirmed, fixed | **verified** | `src/TaskManagement.Application/TaskService.cs:93-98,108-115` (`var saved = false; … finally { if (!saved) dbContext.Entry(task).State = EntityState.Detached; }`) | The `catch` at `:99-107` rethrows, so the `finally` still runs on the translated BR3 path. I re-ran my original F1 probe on the D4 code: `A tracked after call 1: Employee(Bob Chen)=Unchanged, Employee(Alice Morgan)=Unchanged` (no `TaskItem(Race)=Added`); `A call 2 CreateTaskAsync(Valid follow-up, Bob->Alice): ok`; `A call 3 ChangeTaskStatusAsync(...0001, InProgress): ok InProgress`. The same probe on pre-D4 code still gives the old failures (`TaskItem(Race)=Added`, false BR3 naming Alice, raw `DbUpdateException`). Regression test `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` (`TaskServiceTests.cs:341`) is green and goes red without the detach (`d4_red_evidence.txt:1-2`). |
| F2 | confirmed, docs only; schema change deferred to the human | **verified (docs)** | `docs/adr/0009-br3-br4-enforced-by-trigger.md:37-40` | The ADR now says "held to BR4 and to BR3 **on insert**" and names the raw-SQL `assignee_id` gap and the deferral. A grep for the old overclaim found nothing left: `README.md:77` says "trigger on INSERT", `InitialCreate.cs:28` says "BR3: on insert", and `ADR 0002:30` is about BR5 (a CHECK, so true on every write). The Context paragraph (`:10-12`) describes the situation under ADR 0004, and the Consequences section now scopes it. The schema gap stays open by design, waiting for the human, so I do not raise it again. |
| F3 | fixed | **verified** | `TaskService.cs:1-4` | `using System.Threading;` is gone. The build still passes with `-warnaserror` and 0 warnings (`ImplicitUsings`). |
| F4 | **not in the triage** | **not fixed** | `tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs:212,215` | Both lines still use `DateTimeOffset.UtcNow`. `orch-to-D4-001.md:14` says it covers "Every D3 finding", but F4 appears in none of its tables. It is neither fixed nor listed as skipped. It is a nit, so the finding does not matter, but the triage should list it. Either mark it skipped or use the fixed time. |

I also spot-checked the other triage rows that touch this diff against the code. All of them match:
- SF2: `TaskService.cs:150-163` and the test at `TaskServiceTests.cs:365`.
- SF4/C03: `OperationCanceledException` at `:56,132,180`; `DbUpdateException` at `:131`.
- SF5/J-4: the doc at `:52-55` and the test at `TaskServiceTests.cs:396`.
- J-1: the new ordering `[…0001, …0101, …0003]`. Against seed §12, ordering by id gives `[…0001, …0003, …0101]` and ordering by planned start gives `[…0101, …0001, …0003]`, so all three orders differ.
- J-2, J-3, J-5/C01, J-6, J-11.
- J-8/C02: the class doc matches `Contains` for constraints and indexes and `Equal` for triggers at `MigrationAndSeedTests.cs:96-106`.
- C04: `EmployeeConfiguration.cs:14-16`, which agrees with ADR 0005:29.

## 2. New findings introduced by D4

| id | severity | conf. | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| N1 | major | 85 | `src/TaskManagement.Application/TaskService.cs:130` (the `<exception cref="DbUpdateConcurrencyException">` doc); behaviour at `:150-163` | D4's `try/finally` detaches the task **before** the `DbUpdateConcurrencyException` reaches the caller. So the exception's `Entries` are `Detached`. If a caller applies EF's documented concurrency-resolution recipe to the exception this method documents (`GetDatabaseValues` → `OriginalValues.SetValues` → `SaveChanges` again), the retry saves **0 rows and throws nothing**, and the caller's change is silently dropped. Before D4 the same recipe persisted it. The `<exception>` tag at `:130` still describes a plain propagating exception. Only the method remarks (`:134-135`) mention the detach, and they do not say that `ex.Entries` can no longer be resolved and saved again. The documented retry (calling `ChangeTaskStatusAsync` again) works and is tested. | Probe B, same arrangement as `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds` (task `…0001` tracked, another context moves it to `InProgress`, the service tries `Completed`), then the EF recipe on the caught exception.<br>**D4 code:** `B caught DbUpdateConcurrencyException; Entries=1; states=[Detached]` · `B GetDatabaseValuesAsync -> status=InProgress` · `B OriginalValues.SetValues ok; entry.State=Detached; entity Status=Completed` · `B retry SaveChangesAsync -> 0 rows` · `B DB row after recipe: status=InProgress, completed_at=null`<br>**pre-D4 code (`ae7bb74`):** `states=[Modified]` · `B retry SaveChangesAsync -> 1 rows` · `B DB row after recipe: status=Completed, …` | A docs fix. It does not undo the D4 code change. Extend the tag at `:130`: "The task is detached before this propagates, so its `Entries` cannot be resolved and saved again on this context; retry by calling `ChangeTaskStatusAsync` again, which reloads the row." Optional: add the same sentence to `CreateTaskAsync`'s `DbUpdateException` tag (`:52-55`), since its `Entries` are detached too.<br>I found no code fix that keeps both retry paths working. Skipping the detach for `DbUpdateConcurrencyException` brings SF2 back: that is exactly the path `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds` covers, and it goes red without the detach (`d4_red_evidence.txt:3-4`). Reloading the entry instead of detaching it leaves it `Unchanged`, so the recipe would still save 0 rows. The service can therefore support only one retry path. The spec (§14 step 3, written in D4) picks re-calling the method. The docs have to say that on the exception. |

No other new issue reaches the reporting threshold. Checked and clean:
- **Detach placement.** The exception filter at `:99-101` runs before the `finally`. It reads only the exception, so the detach cannot change its result. `saved = true` is set only after the `await` completes. A same-status no-op (`TaskItem.cs:143`) saves nothing and completes, so it never detaches.
- **New tests.** Each theory row gets a fresh database, so the fixed ids `…0102`/`…0103` cannot collide. Every raw-SQL insert satisfies BR1, BR2, BR3 and BR5. The interceptor test builds its context the same way as `PostgresFixture.CreateContext` (`UseNpgsql` only) and adds one interceptor. Dan (`…0004`) has no tasks, so deleting him succeeds, and the insert then fails on the FK (`23503`), not on the trigger. The existing `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger` pins that ordering.
- **Doc edits against the code.** `TaskItem.cs:62-66`: the guard order BR5 → BR3 → BR2 matches `:87,92,99`. `TaskItem.cs:124-128`: BR4 runs before the no-op (`:136` before `:143`), so `Completed → Completed` throws. The `ToUniversalTime()` claims match `:84-85` and `:149`. The `TaskItemStatus` remark is correct (the CHECK and the trigger name the status strings). `CreateTaskAsync` remarks: the service's BR3 check at `:82-87` runs before `TaskItem.Create`, so an inactive self-assignment reports BR3.
- **Out of bounds.** D4 changes no migration, `.csproj`, package or schema, and adds no data-annotation attributes.

## Process observation (outside the scoped path filter)

| id | severity | conf. | file:line | what | evidence | smallest fix |
|---|---|---|---|---|---|---|
| P1 | process (not counted; needs the human's ratification) | 90 on the facts and on the circularity; `[uncertain]` only on whether rule 6 reaches §14/§15 | `.wiki/domain/spec.md` §14 (step 7 of `CreateTaskAsync`, step 3 of `ChangeTaskStatusAsync`) and §15 (7 new rows plus the BR mapping); also `.wiki/domain/br4-final-statuses.md`, `br5-no-self-assignment.md` | Commit `5b0650c` also rewrites the human-approved contract so that it describes D4's own fixes. `git diff --stat ae7bb74 5b0650c -- .wiki/domain` shows 3 files, +19/−7. The spec was approved at the N2 human gate (`ORCHESTRATOR_PROMPT.md:75`), and AGENTS.md rule 6 (`AGENTS.md:28-29`) says to ask the human before "changing the approved data model (`.wiki/domain/`)". `orch-to-D4-001.md` has no disposition row for the spec edit. Only `.wiki/log.md` mentions it ("Spec §14 … and §15 … updated"). `[uncertain]`: the schema sections §10–§12 are unchanged, and it is not clear whether "data model" in rule 6 covers §14 (the service contract) and §15 (the test plan). Either way, a check that "the code matches spec §14" is circular for the detach, because the contract was written in the same commit as the code. | `git log -3 -- .wiki/domain/spec.md` → `5b0650c fix: address confirmed review findings` is the first spec change since `9ae015b` (the grill decisions). | Put the spec and BR-page edits in the human's D4 summary for ratification, and add a row for them to `orch-to-D4-001.md`. No code change. |
