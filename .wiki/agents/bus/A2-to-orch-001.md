---
title: "A2 → orch: unit-test task report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]", "[[index]]"]
from: A2
to: orch
seq: 1
---

# A2 unit tests: report

## Status

Done. Wrote all three test files named in spec §1's "Files per node" and orch-to-A2-001.md, covering every test in
spec §15 "A2 unit tests" (34 test methods, 25 `[InlineData]` theory rows) with exactly the method names the spec
gives. `src/TaskManagement.Domain` is empty in this worktree, as expected — I did not write, and did not need to
guess, any domain code. Compile-checked the tests against a temporary signatures-only stub of the spec §3 API,
built **outside the repo**; it built with 0 warnings, 0 errors. Committed on this worktree's branch.

## Files changed

- `tests/TaskManagement.UnitTests/EmployeeTests.cs` (new)
- `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs` (new)
- `tests/TaskManagement.UnitTests/TaskItemChangeStatusTests.cs` (new)
- `.wiki/agents/bus/A2-to-orch-001.md` (new, this report)

No other file touched. `git diff --stat HEAD~1` (below) confirms exactly these four files.

## Test method → BR / spec row

### `EmployeeTests` (13 methods, 4 theory rows)

| Test method | Spec §15 row | BR |
|---|---|---|
| `Create_ValidInput_SetsPropertiesAndIsActive` | `EmployeeTests` row 1 | — |
| `Create_MixedCaseEmailWithSpaces_StoresTrimmedLowercase` | row 2 | — |
| `Create_NullFullName_ThrowsArgumentNullException` | row 3 | — |
| `Create_BlankFullName_ThrowsArgumentException` (Theory `""`, `"   "`) | row 4 | — |
| `Create_FullNameOf200Chars_Succeeds` | row 5 | — |
| `Create_FullNameOf201Chars_ThrowsArgumentException` | row 5 | — |
| `Create_NullEmail_ThrowsArgumentNullException` | row 6 | — |
| `Create_BlankEmail_ThrowsArgumentException` (Theory `""`, `"   "`) | row 7 | — |
| `Create_EmailOf254Chars_Succeeds` | row 8 | — |
| `Create_EmailOf255Chars_ThrowsArgumentException` | row 8 | — |
| `Create_TwoCalls_ReturnDistinctIds` | row 9 | — |
| `Deactivate_ActiveEmployee_SetsIsActiveFalse` | row 10 | BR3 precondition |
| `Deactivate_InactiveEmployee_StaysInactive` | row 11 | — |

### `TaskItemCreateTests` (16 methods, 5 theory rows)

| Test method | Spec §15 row | BR |
|---|---|---|
| `Create_ValidInput_StartsAsNewWithNullCompletedAt` | row 1 | BR1 |
| `Create_ValidInput_CopiesTitleIdsAndDates` | row 2 | — |
| `Create_TitleWithSurroundingSpaces_StoresTrimmedTitle` | row 3 | — |
| `Create_NullTitle_ThrowsArgumentNullException` | row 4 | — |
| `Create_BlankTitle_ThrowsArgumentException` (Theory `""`, `"   "`) | row 5 | — |
| `Create_TitleOf201Chars_ThrowsArgumentException` | row 6 | — |
| `Create_NullCreator_ThrowsArgumentNullException` | row 7 | — |
| `Create_NullAssignee_ThrowsArgumentNullException` | row 7 | — |
| `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5` | row 8 | BR5 |
| `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3` | row 9 | BR3 |
| `Create_InactiveCreatorActiveAssignee_Succeeds` | row 10 | BR3 scope |
| `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2` | row 11 | BR2 |
| `Create_DueAtEqualsPlannedStartAt_Succeeds` | row 12 | BR2 boundary |
| `Create_PlannedStartAtOrDueAtMissing_Succeeds` (Theory `(true,false)`, `(false,true)`, `(false,false)`) | row 13 | BR2 |
| `Create_NonUtcOffsets_StoresSameInstantWithZeroOffset` | row 14 | time (§6) |
| `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2` | row 15 | BR2 + time |

### `TaskItemChangeStatusTests` (5 methods, 16 theory rows)

| Test method | Spec §15 row | BR |
|---|---|---|
| `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (Theory, 16 `[InlineData]` rows = spec §5 table exactly) | row 1 | BR4 + BR1 |
| `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt` | row 2 | BR1 |
| `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant` | row 3 | BR1 + time |
| `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt` | row 4 | BR4 + BR1 |
| `ChangeStatus_UndefinedStatus_ThrowsArgumentOutOfRangeException` | row 5 | — |

### The 16 transition-table rows (all in `ChangeStatus_TransitionTableRow_BehavesAsSpecified`)

| from | to | allowed | matches spec §5 row |
|---|---|---|---|
| New | New | true | 1 |
| New | InProgress | true | 2 |
| New | Completed | true | 3 |
| New | Cancelled | true | 4 |
| InProgress | New | true | 5 |
| InProgress | InProgress | true | 6 |
| InProgress | Completed | true | 7 |
| InProgress | Cancelled | true | 8 |
| Completed | New | false | 9 |
| Completed | InProgress | false | 10 |
| Completed | Completed | false | 11 |
| Completed | Cancelled | false | 12 |
| Cancelled | New | false | 13 |
| Cancelled | InProgress | false | 14 |
| Cancelled | Completed | false | 15 |
| Cancelled | Cancelled | false | 16 |

All 16 rows present, in the same order as spec §5, with enum values written as `TaskItemStatus.New` etc. (never bare
integers), matching §2a.

**Totals**: 34 `[Fact]`/`[Theory]` test methods, 25 `[InlineData]` theory rows (4 + 5 + 16), covering every row of
spec §15's A2 test list and all 5 rows of the BR → test mapping table (§15) for the A2 column.

## Commands run (real output)

### 1. Read the spec and BR pages, checked repo layout

```
$ ls .wiki/domain/
br1-completed-at.md br2-due-not-before-start.md br3-inactive-assignee.md br4-final-statuses.md
br5-no-self-assignment.md employee.md spec.md task-item.md

$ cat tests/TaskManagement.UnitTests/TaskManagement.UnitTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup><Using Include="Xunit" /></ItemGroup>
  <ItemGroup><ProjectReference Include="..\..\src\TaskManagement.Domain\TaskManagement.Domain.csproj" /></ItemGroup>
</Project>

$ ls src/TaskManagement.Domain/
TaskManagement.Domain.csproj        # only the csproj; no .cs files, as expected (A1 not started yet)
```

### 2. Compile check: signatures-only stub built OUTSIDE the repo (never committed)

Built at `%TEMP%\claude\E--BSS-TT\...\scratchpad\a2-stub-check`, a throwaway copy of
`Directory.Build.props`, `.editorconfig`, my three test files, and a hand-written signatures-only stub of the
spec §3 API (`Employee`, `TaskItem`, `TaskItemStatus`, `BusinessRuleViolationException`; public members exactly as
spec §3 lists them, non-trivial bodies `throw new NotImplementedException()`).

```
$ cd .../scratchpad/a2-stub-check/tests/TaskManagement.UnitTests && dotnet build
  Determining projects to restore...
  Restored .../a2-stub-check/src/TaskManagement.Domain/TaskManagement.Domain.csproj (in 68 ms).
  Restored .../a2-stub-check/tests/TaskManagement.UnitTests/TaskManagement.UnitTests.csproj (in 434 ms).
  TaskManagement.Domain -> .../a2-stub-check/src/TaskManagement.Domain/bin/Debug/net10.0/TaskManagement.Domain.dll
  TaskManagement.UnitTests -> .../a2-stub-check/tests/TaskManagement.UnitTests/bin/Debug/net10.0/TaskManagement.UnitTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.49
```

0 warnings / 0 errors: no CS/analyzer/xUnit-analyzer diagnostic, so the test files are syntactically and
type-correct against the spec §3 signatures (exact member names, parameter types and order, exception constructors,
`RuleId` property, `TaskItemStatus` values) and satisfy `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended`
+ CS1591 (XML docs present on every public member, including the stub's, so this also validates my test files'
own doc coverage — the stub's docs were written to compile, the failure mode I was checking for is in my test
files, not the stub). The stub itself, and its build output, were not committed and do not exist in the repo.

### 3. Verification counts

```
$ grep -c '\[Fact\]' tests/TaskManagement.UnitTests/*.cs
EmployeeTests.cs:11
TaskItemChangeStatusTests.cs:4
TaskItemCreateTests.cs:14

$ grep -c '\[Theory\]' tests/TaskManagement.UnitTests/*.cs
EmployeeTests.cs:2
TaskItemChangeStatusTests.cs:1
TaskItemCreateTests.cs:2

$ grep -c '\[InlineData' tests/TaskManagement.UnitTests/*.cs
EmployeeTests.cs:4
TaskItemChangeStatusTests.cs:16
TaskItemCreateTests.cs:5
```

11+2=13 (Employee), 14+2=16 (TaskItemCreate), 4+1=5 (TaskItemChangeStatus) → 34 test methods total; 4+5+16=25
theory rows total.

### 4. Commit

```
$ git add tests/TaskManagement.UnitTests/EmployeeTests.cs tests/TaskManagement.UnitTests/TaskItemCreateTests.cs \
    tests/TaskManagement.UnitTests/TaskItemChangeStatusTests.cs .wiki/agents/bus/A2-to-orch-001.md
$ git commit -m "test(domain): unit tests for BR1-BR5 and the transition table"
[worktree-agent-ab046b2aed9af8b7f 063970d] test(domain): unit tests for BR1-BR5 and the transition table
 4 files changed, 833 insertions(+)
 create mode 100644 .wiki/agents/bus/A2-to-orch-001.md
 create mode 100644 tests/TaskManagement.UnitTests/EmployeeTests.cs
 create mode 100644 tests/TaskManagement.UnitTests/TaskItemChangeStatusTests.cs
 create mode 100644 tests/TaskManagement.UnitTests/TaskItemCreateTests.cs
```

Note: this report file was staged and committed in the same commit as the tests, so the diff below (taken after
the commit) includes the report's own line count — the acceptance line "touches only your 3 test files and your
report" holds.

### 5. Post-commit evidence

```
$ git log --oneline -1
063970d test(domain): unit tests for BR1-BR5 and the transition table

$ git rev-parse HEAD
063970d4fd32e2fef69b9ee79eff0818e06c2d9f

$ git rev-parse --abbrev-ref HEAD
worktree-agent-ab046b2aed9af8b7f

$ git diff --stat HEAD~1
 .wiki/agents/bus/A2-to-orch-001.md                 | 224 +++++++++++++++++
 tests/TaskManagement.UnitTests/EmployeeTests.cs    | 188 ++++++++++++++
 .../TaskItemChangeStatusTests.cs                   | 147 +++++++++++
 .../TaskItemCreateTests.cs                         | 274 +++++++++++++++++++++
 4 files changed, 833 insertions(+)
```

## Worktree branch

`worktree-agent-ab046b2aed9af8b7f`

## `[uncertain]` items

None. Every member, exception type, constructor overload and `RuleId` string used in these tests is copied
character-for-character from spec §3/§3.2/§3.3/§3.4, and the stub compile check (§2 above) confirms the test code
is well-formed against exactly that shape with zero analyzer diagnostics.

## Open questions

None for orch. The tests are self-contained against the spec; nothing here depends on an A1 implementation choice
that the spec left open. If A1's actual code disagrees with spec §3 in some way the stub could not catch (for
example a guard-order difference that only a runtime failure would reveal), that will surface as a real (not
stub) build/test failure at fan-in A, not as a defect in this test suite.
