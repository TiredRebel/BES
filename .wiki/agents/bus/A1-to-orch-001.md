---
title: "A1 → orch: domain task report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[task-item]]", "[[index]]"]
from: A1
to: orch
seq: 1
---

# A1 Domain: report

## Status

Done.

## Files changed

- `src/TaskManagement.Domain/Employee.cs` (new)
- `src/TaskManagement.Domain/TaskItem.cs` (new)
- `src/TaskManagement.Domain/TaskItemStatus.cs` (new)
- `src/TaskManagement.Domain/BusinessRuleViolationException.cs` (new)
- `.wiki/agents/bus/A1-to-orch-001.md` (this report, new)

## Public API implemented

```csharp
namespace TaskManagement.Domain;

public sealed class Employee
{
    public const int FullNameMaxLength = 200;
    public const int EmailMaxLength = 254;

    public Guid Id { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }

    public static Employee Create(string fullName, string email);
    public void Deactivate();
}

public sealed class TaskItem
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public Guid CreatorId { get; private set; }
    public Guid AssigneeId { get; private set; }
    public DateTimeOffset? PlannedStartAt { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public uint Version { get; private set; }

    public static TaskItem Create(
        string title,
        Employee creator,
        Employee assignee,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? dueAt);

    public void ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt);
}

public enum TaskItemStatus
{
    New = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public sealed class BusinessRuleViolationException : InvalidOperationException
{
    public BusinessRuleViolationException(string ruleId, string message);
    public BusinessRuleViolationException(string ruleId, string message, Exception innerException);

    public string RuleId { get; }
}
```

Both entities have a `private` parameterless constructor (EF materialisation); non-nullable strings are
initialised to `string.Empty` there. All properties are `private set`. No data-annotation attributes, no
navigation properties.

`TaskItem.Create` guard order (matches spec §3.2 exactly): title null/whitespace →
title length → creator null → assignee null → UTC-normalise both dates → BR5 (`creator.Id == assignee.Id`) →
BR3 (`!assignee.IsActive`) → BR2 (`dueAt < plannedStartAt`, only when both non-null).

`TaskItem.ChangeStatus` order: unknown enum value (`ArgumentOutOfRangeException`) → BR4 (current status
`Completed`/`Cancelled`) → no-op if `newStatus == Status` → apply (`Status = newStatus`, `CompletedAt` set to
`changedAt.ToUniversalTime()` iff moving to `Completed`, else `null`).

## Commands run (real output)

```
$ dotnet build src/TaskManagement.Domain -warnaserror
  Determining projects to restore...
  All projects are up-to-date for restore.
  TaskManagement.Domain -> E:\BSS TT\.claude\worktrees\agent-a61dc97863a0f8a63\src\TaskManagement.Domain\bin\Debug\net10.0\TaskManagement.Domain.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.09
Exit code: 0
```

Re-run after adding the missing `<remarks>` on `TaskItem.Create` (see "Amendment" below); same result on the
first pass (build tail then: "Restored ... in 58 ms", 0 Warning(s), 0 Error(s), Time Elapsed 00:00:02.56).

```
$ grep -rnE '\[(Key|Required|Table|Column|MaxLength|StringLength|ForeignKey|Index|NotMapped|DatabaseGenerated|ConcurrencyCheck|Timestamp|InverseProperty|Owned|Precision|Unicode)\b' src/TaskManagement.Domain --include=*.cs
(no output — grep exit code 1, i.e. no matches, so the spec's `!` negation makes the acceptance check exit 0)
```

```
$ git rev-parse --abbrev-ref HEAD
worktree-agent-a61dc97863a0f8a63
```

```
$ git log --oneline -3
3d9ef4d docs(bus): fill in A1 report evidence and TaskItem.Create remarks
d1a406a feat(domain): entities, status enum and business-rule exception
a6b3933 docs(bus): wave A task specs for A1 and A2

$ git diff --stat HEAD~1
 .wiki/agents/bus/A1-to-orch-001.md    | 45 ++++++++++++++++++++++++++---------
 src/TaskManagement.Domain/TaskItem.cs |  4 ++++
 2 files changed, 38 insertions(+), 11 deletions(-)

$ git diff --stat HEAD~2        # cumulative A1 delta (feature commit + this evidence commit)
 .wiki/agents/bus/A1-to-orch-001.md                 | 166 +++++++++++++++++++++
 .../BusinessRuleViolationException.cs              |  45 ++++++
 src/TaskManagement.Domain/Employee.cs              |  80 ++++++++++
 src/TaskManagement.Domain/TaskItem.cs              | 146 ++++++++++++++++++
 src/TaskManagement.Domain/TaskItemStatus.cs        |  23 +++
 5 files changed, 460 insertions(+)
```

Two commits carry A1's work: `d1a406a` (the four domain files + first draft of this report) and
`3d9ef4d` (added the missing `<remarks>` to `TaskItem.Create` and filled in this evidence section). A
report committed as part of the history it describes cannot show its own commit's stat in advance, so this
final version is written and committed as a third, separate change (see this file's own commit) rather than
predicted ahead of time.

## [uncertain] items

- None found while implementing. All names, signatures, guard order and exception types were copied verbatim
  from `.wiki/domain/spec.md` §3, §3.1, §3.2, §3.3, §3.4 and cross-checked against `.wiki/domain/employee.md`
  and `.wiki/domain/task-item.md`; no discrepancy between the two sources was found.

## Open questions

- **`.wiki/log.md` and `.wiki/domain/employee.md` / `task-item.md`**: `AGENTS.md` Workflow says "Every commit
  appends an entry to `.wiki/log.md`" and "A commit that changes an entity ... updates that entity's page in
  `.wiki/domain/` in the same commit." My task spec's "Files you create (only these)" list and "Do not touch"
  section list only the four domain files plus my report, and explicitly says "all docs outside your report"
  are off limits. I followed the task spec as written and did not touch `.wiki/log.md` or the domain entity
  pages (both already describe `Employee`/`TaskItem` accurately as "planned", which is now true). Flagging so
  orch (or whichever node owns fan-in bookkeeping) adds the `log.md` entry and updates entity-page status if
  that's expected before merge.

Everything else in the spec was unambiguous for what A1 owns (Employee, TaskItem, TaskItemStatus,
BusinessRuleViolationException). Did not touch Infrastructure, Application, tests, csproj files,
Directory.Build.props or .editorconfig, per the "do not touch" list.
