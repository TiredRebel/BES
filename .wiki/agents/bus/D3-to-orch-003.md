---
title: "D3 type-design-analyzer → orch: domain type review"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[orch-to-D3-001]]", "[[index]]", "[[log]]"]
from: D3-type-design-analyzer
to: orch
seq: 3
---

# D3 review 3: type design (domain types)

Scope: `src/TaskManagement.Domain/{Employee,TaskItem,TaskItemStatus,BusinessRuleViolationException}.cs` on
`feature/task-management` (the working-tree files match the branch: `git show feature/task-management:<file>` diffs
empty for both entities). Contract: `.wiki/domain/spec.md` §3, §5, §8, §11, §14; ADR 0005, 0008, 0009. To see what
EF materialisation and EF writes can reach, I also read `TaskItemConfiguration.cs`, `EmployeeConfiguration.cs`, the
`InitialCreate` trigger SQL and `TaskService.cs`. Evidence file used: `scratchpad\d3\fa_red_evidence.txt`. I did not
read the workers' bus reports, did not build or run anything, and edited no code.

**Verdict:** 0 critical, 0 major, 1 minor, 2 nit. No domain API path can create or reach a BR1–BR5 violation. The
one reachable BR3 gap goes around the domain: an UPDATE of `assignee_id`, through EF's change tracker or raw SQL,
that neither the trigger nor any CHECK looks at.

## Findings

| id | severity | conf. | file:line | what is wrong | evidence | smallest fix |
|---|---|---|---|---|---|---|
| T1 | minor | 75 (mechanism 95; 75 that it counts as a BR3 breach) | `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs:148`, `:168`; `src/TaskManagement.Domain/TaskItem.cs:28-30` | BR3 is checked only on INSERT. `AssigneeId` is "Never changes" in the domain (no setter after `Create`), but the database doesn't enforce that, and the trigger never re-checks BR3 when `assignee_id` changes. A task can therefore be moved to an inactive employee. That contradicts ADR 0009 ("Every writer is held to BR3 and BR4, raw SQL included"). Spec §8 rests on "Creation is the only moment `AssigneeId` is set (no reassignment)". The domain keeps that promise, but EF and raw SQL don't have to. | Trigger: `BEFORE INSERT OR UPDATE OF status` (`:168`), and the BR3 branch is gated `IF TG_OP = 'INSERT' THEN` (`:148`). An UPDATE that doesn't set `status` doesn't fire the trigger, and even when it fires, the UPDATE never reaches the BR3 branch. **EF path** (public API, no raw SQL): `var t = await db.Tasks.SingleAsync(x => x.Id == task1); db.Entry(t).Property(x => x.AssigneeId).CurrentValue = carolId; await db.SaveChangesAsync();`. EF sends an UPDATE of `assignee_id` with the `xmin` predicate. The FK passes (Carol exists), `ck_tasks_br5_assignee_not_creator` passes (Alice ≠ Carol), and no BR3 check runs. **Raw SQL**: `UPDATE tasks SET assignee_id = '10000000-0000-0000-0000-000000000003' WHERE id = '20000000-0000-0000-0000-000000000001';` has the same outcome. Result: Carol (seeded `IsActive = false`) holds a `New` task. Found by reading the code; neither path was run. | Pick one. (a) EF only, no migration change: `builder.Property(t => t.AssigneeId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);`, and the same for `CreatorId`, in `TaskItemConfiguration`. `IMutableProperty.SetAfterSaveBehavior` and `PropertySaveBehavior.Throw` exist in the EF 10.0.12 XML docs. `[uncertain]` whether this touches the model snapshot. (b) Every writer: change the trigger to `UPDATE OF status, assignee_id` and run BR3 in its own `IF` block (not the `ELSIF`) when `TG_OP = 'INSERT' OR NEW.assignee_id IS DISTINCT FROM OLD.assignee_id`. This changes spec §11's pinned SQL, so it needs an amendment. (c) At minimum, reword ADR 0009's "every writer" claim to "every insert". |
| T2 | nit | 95 | `src/TaskManagement.Domain/BusinessRuleViolationException.cs:18-22`, `:35-39`, `:44` | `RuleId` has a documented domain (`"BR2" \| "BR3" \| "BR4" \| "BR5"`) that nothing enforces. Tests and callers branch on it. It doesn't break BR1–BR5. | `RuleId = ruleId;` with no check: `new BusinessRuleViolationException("BR9", "x")` and `("", "x")` compile and construct. A `null` literal is blocked only by nullable warnings (errors in this repo). ADR 0008 deliberately accepts string literals ("a constants class is not worth a file"), and spec §3.4 pins `string RuleId`, so this stays a nit. | None required. If wanted: `ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);` in both constructors. |
| T3 | nit | 90 | `src/TaskManagement.Domain/TaskItem.cs:11-14`, `:19`; `src/TaskManagement.Domain/Employee.cs:14-18`, `:23`, `:26` | Materialisation trusts the row: EF uses the private constructor and private setters, and the types re-validate nothing. For BR1/2/3/4/5 and the status value the database guarantees the row, so no BR state is reachable (see the matrix). The non-BR invariants the docs claim have no database backing: `Title` "trimmed … (1..200 characters)", `FullName` trimmed, `Email` lower-invariant. | `INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES ('30000000-0000-0000-0000-0000000000aa', '', 'New', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', NULL, NULL, NULL)` passes every constraint in §11 (`character varying(200) NOT NULL` accepts `''`) and loads as a `TaskItem` with `Title == ""`. ADR 0005 already accepts this for `Email`. Spec §11 says the migration has "exactly these, no more" constraints, so a CHECK would be a spec change. | None now. If it matters later: a `title <> ''`/`btrim(title) = title` CHECK via a spec amendment. Otherwise scope the doc to "when created through `Create`". |

## BR × path reachability matrix

Paths: the domain API (`TaskItem.Create`, `TaskItem.ChangeStatus`, `Employee.Deactivate`), EF materialisation
(private constructor plus property writes from a row), and EF or raw-SQL writes that go around the domain
(`Entry(...).Property(...).CurrentValue`, `ExecuteSqlRaw`).

| Rule | Domain API | EF materialisation | EF / raw-SQL write around the domain |
|---|---|---|---|
| BR1 | Closed. `TaskItem.cs:143-144` writes `Status` and `CompletedAt` in one statement. `Create` sets `New`/`null` (`:107`, `:112`). No path sets one without the other, and a move to a non-`Completed` status also clears `CompletedAt`. | Closed by `ck_tasks_br1_completed_at_iff_completed` (`TaskItemConfiguration.cs:48`) | Closed by the same CHECK |
| BR2 | Closed. Dates are normalised (`:83-84`) before the instant compare (`:98`). No setter for `PlannedStartAt`/`DueAt` after `Create`, and `ChangeStatus` doesn't touch them. | Closed by `ck_tasks_br2_…` (`:49`) | Closed by the same CHECK |
| BR3 | Closed for creation. `:91` checks `assignee.IsActive` on the object it is handed, which can be stale. That staleness is closed by the service check (`TaskService.cs:73`) and the insert trigger (migration `:148-155`), per spec §8. `Deactivate` is one-way (no `Activate`). | n/a (materialising doesn't assign anything) | **Open for UPDATE of `assignee_id` (T1).** Closed for INSERT by the trigger. |
| BR4 | Closed. The guard (`:131`) runs before the same-status no-op (`:138`), so `Completed→Completed` and `Cancelled→Cancelled` throw BR4 exactly as the spec §5 table requires. This is intentional, not a bug. | n/a (materialising doesn't transition) | Closed by the trigger for any SET list that includes `status`. Changing only `completed_at` on a `Completed` row is allowed by design (spec §11: "An update of another column on a Completed task … was accepted"), and BR1 still holds because the CHECK keeps it non-null. |
| BR5 | Closed. Compared by `Id`, not by reference (`:86`). Neither id has a setter after `Create`. | Closed by `ck_tasks_br5_assignee_not_creator` (`:50`) | Closed by the same CHECK |
| Undefined status | Closed. `Status` has exactly two writers: `Create` (constant `New`) and `ChangeStatus`, guarded by `Enum.IsDefined` (`:126`) before the BR4 guard, per spec §3.2. `default(TaskItemStatus)` is `New = 0`, a safe non-final value. | Closed by `ck_tasks_status_valid` (`:47`). `[uncertain]` what EF's `HasConversion<string>()` converter does with a string outside the four names (throw, or `default`). The CHECK makes that unreachable, so I didn't verify it. | Closed by the same CHECK |

The private parameterless constructors produce instances that break invariants (`TaskItem`: `CreatorId ==
AssigneeId == Guid.Empty` and `Title == ""`). Only reflection or EF can call them, and EF overwrites every mapped
column, so no public path leads there.

## Ratings

### Type: `TaskItem`

**Invariants identified:** title trimmed, 1..200; `Status` is a defined member; BR1 (`CompletedAt` non-null iff
`Completed`); BR2 (`DueAt >= PlannedStartAt` when both set); BR3 (assignee active at creation); BR4 (`Completed` and
`Cancelled` are final); BR5 (`AssigneeId != CreatorId`); `CreatorId`/`AssigneeId`/dates immutable after `Create`;
every timestamp has offset 0; `Version` is never written by the domain.

- **Encapsulation: 9/10.** Sealed, private constructor, static factory, every property `private set`, no
  collections, only value types and a string. The one way around it is EF's change tracker (T1), which the type
  cannot prevent on its own.
- **Invariant Expression: 7/10.** The BR1 coupling is carried by one assignment line plus the doc, not by the shape
  (`Status` plus `DateTimeOffset?` can still represent `Completed`/`null`). A per-status type would make that
  unrepresentable, but it would be over-engineering for a four-member lifecycle that is backed by CHECK and trigger.
  The limits are shared with EF as `const`.
- **Invariant Usefulness: 9/10.** Every invariant maps to a named BR or a column limit. The transition rule is
  exactly as permissive as the brief allows (BR4 only).
- **Invariant Enforcement: 8/10.** Every guard is checked at construction or mutation, and each one turns tests red
  when removed (`fa_red_evidence.txt`: BR5 removed → 1 red, BR3 → 1, BR2 → 2, BR4 → 9, BR1 assignment → 5). The
  deductions are the materialisation trust (T3) and immutability of `AssigneeId` that holds only in the domain (T1).

**Strengths:** guard order matches spec §3.2 line for line (`:73-101`, `:126-144`). The domain never reads a clock
(`changedAt` is passed in). The same-status no-op is only reachable for non-final statuses.
**Concerns:** T1, T3. `changedAt` has no bounds (`default(DateTimeOffset)` or a time before creation is accepted).
That isn't a BR, so it isn't a finding.
**Recommended improvements:** T1 option (a) or (b). Nothing else is worth its cost.

### Type: `Employee`

**Invariants identified:** full name trimmed, 1..200; e-mail trimmed, lower-invariant, 1..254 (checked after
normalisation, `Employee.cs:54-55`); active on creation; `Deactivate` is one-way and idempotent.

- **Encapsulation: 9/10.** Sealed, private constructor and setters, a single mutator.
- **Invariant Expression: 7/10.** Normalisation and limits are visible only in the docs and the `const`s. `Email`
  is a plain `string`. That's fine at this size.
- **Invariant Usefulness: 8/10.** `IsActive` exists for BR3, and the lowercase e-mail exists for `ux_employees_email`
  (ADR 0005).
- **Invariant Enforcement: 8/10.** `Create` validates everything. Case-insensitive e-mail uniqueness is not
  database-backed for raw writes (ADR 0005 accepts this; T3).

**Concerns:** only T3. No `Activate` (YAGNI, spec §3.1), so BR3 needs no guard in `Deactivate`.

### Type: `TaskItemStatus`

**Invariants identified:** four members with exact names (they are stored as text and matched by
`ck_tasks_status_valid`); `Completed` and `Cancelled` are final.

- **Encapsulation: 6/10.** A C# enum can't be closed: `(TaskItemStatus)99` compiles. Both entry points guard against
  it (`TaskItem.cs:126`, `TaskService.cs:147`), and the database CHECK backs them.
- **Invariant Expression: 7/10.** Explicit values, and the docs name the final members. Finality lives in
  `TaskItem.ChangeStatus` (`:131`) and in the trigger SQL, not on the enum. There is no `IsFinal` helper, and no caller
  needs one yet.
- **Invariant Usefulness: 8/10.**
- **Invariant Enforcement: 8/10.** `default` is the safe `New`, and every writer is guarded.

**Strengths:** the explicit values and the double-L spelling line up with the stored text and the CHECK.
**Concerns / improvements:** none required. Add `IsFinal` only when a second caller needs it.

### Type: `BusinessRuleViolationException`

**Invariants identified:** `RuleId` ∈ {BR2, BR3, BR4, BR5}; never thrown for BR1.

- **Encapsulation: 8/10.** Sealed, get-only `RuleId`.
- **Invariant Expression: 5/10.** The id domain is stated only in XML docs (T2).
- **Invariant Usefulness: 8/10.** Lets callers and tests tell the rules apart without parsing messages (ADR 0008).
- **Invariant Enforcement: 4/10.** Nothing validates `ruleId`. The four in-repo call sites all pass correct literals
  (`TaskItem.cs:88`, `:94`, `:100`, `:134`; `TaskService.cs:76`, `:93`), and the tests assert on them.

**Strengths:** one type for "a rule was broken", with the inner exception preserved on the trigger path.
**Concerns / improvements:** T2 only (optional one-line guard).

## Checked and found clean

- All four types are `sealed` (the enum trivially so). No public constructor on either entity, and no public setter
  anywhere (`TaskItem.cs:6`, `:11`, `:17-44`; `Employee.cs:6`, `:14`, `:21-31`).
- `Create` guard order is title → length → nulls → UTC normalisation → BR5 → BR3 → BR2, as spec §3.2 pins it.
  `TaskServiceTests.CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` depends on BR5
  coming before BR3.
- `ChangeStatus` order is `Enum.IsDefined` → BR4 → no-op → assignment, as spec §3.2 pins it. After a BR4 throw the
  state is unchanged, because the throw comes before any write.
- `TaskItem.Create` with an `Employee` that was never persisted: the ids are fresh Version 7 GUIDs, so the insert
  fails on `fk_tasks_employees_assignee_id`/`…creator_id`. That isn't a BR state.
- A `Deactivate()` and a task insert in one `SaveChanges`: whichever order EF sends them, the outcome is consistent
  with BR3. Either the trigger sees `is_active = false` and rejects the insert, or the task existed before the
  deactivation, which spec §8 allows.
- Title length check: `.Length` counts UTF-16 units and `character varying(200)` counts characters, so the domain is
  stricter and a domain-valid title never overflows the column.
- UTC: every incoming `DateTimeOffset`, including `changedAt` (`:144`), goes through `ToUniversalTime()`, and
  `timestamptz` reads back with offset 0.
- EF writes around the domain on `Status`, `CompletedAt`, `DueAt`/`PlannedStartAt` or `CreatorId = AssigneeId` are
  rejected by the trigger or the CHECKs (matrix above). Only `assignee_id` gets through (T1).

## Outside my lens (pointers for orch, no rating)

- `TaskService.cs:82-96`: after the BR3 trigger error is translated, the `TaskItem` presumably stays in the change
  tracker as `Added` (`[uncertain]`: I assume EF leaves entry states unchanged when `SaveChanges` fails, and I didn't
  verify it). A caller that reuses the context and saves again would retry the insert and get an untranslated
  `DbUpdateException`. That's for silent-failure-hunter or code-reviewer.
- `ListTasksByAssigneeAsync` returns mutable, untracked `TaskItem`s, so calling `ChangeStatus` on them persists
  nothing. Spec §14 accepts this and the XML docs say so. No finding.
