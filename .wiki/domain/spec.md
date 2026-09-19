---
title: Data model and contracts (N1 spec)
type: spec
status: approved
updated: 2026-09-18
related: ["[[employee]]", "[[task-item]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]", "[[decisions/index]]", "[[index]]"]
---

# Task Management data layer: spec

The contract that A1, A2, B1, B2 and B3 code against in parallel. Every name in `code font` is exact, character for
character. Every code path here is **planned**: none exists yet. `[uncertain]` marks what was not proven by a file,
a doc page or a command; section 16 says where everything else was verified.

The module: **Task Management**, the internal CRM module for assigning, executing and controlling employees' tasks.
Its three jobs map onto the three use cases:

| Job | Use case (§14) | Rules |
|---|---|---|
| Assigning: a creator gives a task to an employee | `CreateTaskAsync` | BR2, BR3, BR5 |
| Executing: the task moves through its statuses | `ChangeTaskStatusAsync` | BR1, BR4 |
| Controlling: what does an employee have, in which status, due when | `ListTasksByAssigneeAsync` (optional status filter, ordered by deadline) | uses `ix_tasks_assignee_id_status` |

Scope is the brief only: employees, tasks, assignment, deadlines, statuses, BR1–BR5 and these three use cases.
Anything else is out.

## 1. Solution layout

ADR: [0001](../../docs/adr/0001-solution-layout.md).

| Project (path) | Holds | Project references | Package references |
|---|---|---|---|
| `src/TaskManagement.Domain/TaskManagement.Domain.csproj` | entities, enum, domain exception | none | none |
| `src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj` | DbContext, Fluent configs, seed, migrations, design-time factory | Domain | `Microsoft.EntityFrameworkCore` 10.0.12; `Microsoft.EntityFrameworkCore.Relational` 10.0.12 (approved by the human at N2, see below); `Microsoft.EntityFrameworkCore.Design` 10.0.12 with `<PrivateAssets>all</PrivateAssets>` and `<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`; `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 |
| `src/TaskManagement.Application/TaskManagement.Application.csproj` | `TaskService` (the three use cases) | Domain, Infrastructure | none (EF Core arrives transitively from Infrastructure) |
| `tests/TaskManagement.UnitTests/TaskManagement.UnitTests.csproj` | A2: domain unit tests, `[Trait("Category", "Unit")]` | Domain | `Microsoft.NET.Test.Sdk` 17.14.1; `xunit` 2.9.3; `xunit.runner.visualstudio` 3.1.4 |
| `tests/TaskManagement.IntegrationTests/TaskManagement.IntegrationTests.csproj` | B3: Testcontainers tests, `[Trait("Category", "Integration")]` | Domain, Infrastructure, Application | same three as UnitTests + `Testcontainers.PostgreSql` 4.15.0 |

- Solution file: `TaskManagement.slnx` at the repo root (S0 creates it; `.slnx` is what graph.yaml already names).
- Both test projects: `<IsPackable>false</IsPackable>` and `<Using Include="Xunit" />`, as in the N0 probe's test
  project. Target framework, nullable, analyzers and docs come from `Directory.Build.props`; the csproj files do not
  repeat them.
- **Two test projects, not one**: the unit project references only Domain, so `dotnet test --filter Category=Unit`
  needs neither EF Core nor Docker, and A2 cannot accidentally lean on persistence. Traits stay on every test anyway.
  At solution level the filter exits 0 even though the integration project matches zero tests: VSTest prints
  "No test matches" for that project (verified by orch on the N0 probe; see `.wiki/log.md`).
- **Relational pin.** Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3's nuspec declares
  `Microsoft.EntityFrameworkCore.Relational [10.0.4, 11.0.0)`. Design 10.0.12 (a `developmentDependency`, so it does
  not flow) declares Relational 10.0.12. Consumers of Infrastructure would resolve 10.0.4 while Infrastructure was
  compiled against 10.0.12, which is the MSB3277 that N0 hit. The explicit Relational 10.0.12 reference goes in
  Infrastructure only and flows to Application and IntegrationTests. If the human rejects it, the spec has no
  approved alternative: stop and ask.
- Test files live under `tests/`, so the `.editorconfig` section `[tests/**.cs]` turns CA1707 off for
  `Method_Scenario_Expected` names. The N0 probe proved that glob matches `tests/Probe.Tests/Smoke.cs`.

### Files per node (exact)

| Node | Files it creates |
|---|---|
| A1 | `src/TaskManagement.Domain/Employee.cs`, `TaskItem.cs`, `TaskItemStatus.cs`, `BusinessRuleViolationException.cs` |
| A2 | `tests/TaskManagement.UnitTests/EmployeeTests.cs`, `TaskItemCreateTests.cs`, `TaskItemChangeStatusTests.cs` |
| B1 | `src/TaskManagement.Infrastructure/TaskManagementDbContext.cs`, `TaskManagementDbContextFactory.cs`, `Configurations/EmployeeConfiguration.cs`, `Configurations/TaskItemConfiguration.cs`, `Migrations/<timestamp>_InitialCreate.cs` + `.Designer.cs` + `TaskManagementDbContextModelSnapshot.cs` (generated by `dotnet ef`, then a header comment added to `InitialCreate`) |
| B2 | `src/TaskManagement.Application/TaskService.cs` |
| B3 | `tests/TaskManagement.IntegrationTests/PostgresFixture.cs`, `MigrationAndSeedTests.cs`, `DatabaseConstraintTests.cs`, `TaskServiceTests.cs` |

## 2. Naming traps

`ImplicitUsings` imports `System.Threading.Tasks`. So:

- The task entity is **`TaskItem`**, never `Task`.
- The status enum is **`TaskItemStatus`**, never `TaskStatus`.
- No type, namespace segment or file in this repo is named `Task`, `Tasks` or `TaskStatus`. The only thing called
  `Tasks` is the `DbSet<TaskItem>` property on the DbContext, which does not collide (it is a member, not a type).
- The table is still called `tasks`: SQL names are free.
- Analyzer rules that are **errors** here (`latest-recommended` + `TreatWarningsAsErrors`; checked in
  `analysislevel_10_recommended.globalconfig`) and bite this model:
  - CA1510: `ArgumentNullException.ThrowIfNull(x)`, never `if (x is null) throw new ArgumentNullException(...)`.
  - CA2208: `new ArgumentException(message, paramName)` and `new ArgumentOutOfRangeException(paramName, actualValue, message)`,
    with `paramName` equal to a real parameter name.
  - CA2201: never `throw new Exception(...)`.
  - CA1710 / CA1711: an exception type ends in `Exception`; no other type ends in `Collection`, `Dictionary`,
    `Queue`, `Stack`, `EventHandler` or `Exception`. B3: the xUnit collection-definition class must not end in
    `Collection` (use `PostgresCollectionDefinition`).
  - CA1716 (keywords), CA1720 (type names in identifiers), CA1805 (no explicit `= false`/`= 0`/`= null` initialisers),
    CA1822, CA1852, CA2016 (forward the `CancellationToken`), IDE0044 (`dotnet_style_readonly_field = true:warning`:
    private fields that are never reassigned must be `readonly`).
  - Also errors, and likely in tests and fixtures: CA1861 (constant arrays passed as arguments; use `static readonly`
    fields), CA1859, CA1816, CA1051 (no visible instance fields), CA1001 (a type that owns a disposable field must be
    disposable). `xunit` 2.9.3 brings `xunit.analyzers` 1.18.0, whose warnings are errors too: for example, every
    `[Theory]` needs data, and `[InlineData]` values must match the parameter types.
  - **Not** enabled (in `_all`, absent from `_recommended`): CA1032 (standard exception constructors), CA1062, CA1515,
    CA2007 (`ConfigureAwait`), CA1308, CA1002. No need to satisfy them.
- EF Core 10: `EntityTypeBuilder<T>.HasCheckConstraint(...)` is `[Obsolete]` ("Configure this using
  ToTable(t => t.HasCheckConstraint()) instead."), which is a build error here. Use
  `builder.ToTable("tasks", t => t.HasCheckConstraint(name, sql))`. `IndexBuilder.HasName` is also obsolete: use
  `HasDatabaseName`.
- Testcontainers 4.15.0: the parameterless `new PostgreSqlBuilder()` is `[Obsolete]`. Use
  `new PostgreSqlBuilder("postgres:17-alpine")`.

## 2a. Documentation standard (every worker, every file)

From the brief's DOCUMENTATION STANDARD. With `GenerateDocumentationFile` and CS1591 as an error, a public type or
member without an XML doc comment fails the build. That includes test classes, test methods, the fixture, and the
enum members and exception shown as bare shapes in §3.3 and §3.4.

- Every type and member: `<summary>`, plus `<param>` for each parameter, `<returns>` for non-void members, and
  `<exception cref="...">` for every exception it throws or lets through.
- Where code enforces a business rule, a `<remarks>` names it (for example `Enforces BR5: ...`). This applies to
  guards, CHECK constraints, the service check, and tests (a test's `<summary>` or `<remarks>` names the BR it proves).
- Each Fluent configuration class documents in its XML docs which indexes and constraints it creates and why
  (the name from §11 and the BR or brief item it serves).
- The migration `InitialCreate` starts with a header comment listing its schema changes: tables, columns, PKs, FKs,
  indexes, CHECK constraints, seed rows.
- Overrides and interface implementations may use `/// <inheritdoc />`.
- `[InlineData]` values must match the parameter types exactly (xUnit1010/xUnit1012 are errors): write enum arguments
  as `TaskItemStatus.New`, never as `0`, and give the transition theory the signature
  `(TaskItemStatus from, TaskItemStatus to, bool allowed)`.

## 3. Entities (namespace `TaskManagement.Domain`)

No data-annotation attributes. No navigation properties: foreign keys are plain `Guid` properties and the
relationships are configured in B1 with `HasOne<Employee>().WithMany()`. All properties have `private set`. Each
entity has a `private` parameterless constructor for EF Core materialisation (non-nullable strings initialised so
CS8618 stays quiet, for example `= string.Empty`, which CA1805 does not flag). Details: [[employee]], [[task-item]].

### 3.1 `Employee` (`public sealed class Employee`)

| Member | Type | Notes |
|---|---|---|
| `public const int FullNameMaxLength = 200;` | `int` | shared with B1 (`HasMaxLength`) |
| `public const int EmailMaxLength = 254;` | `int` | shared with B1 |
| `Id` | `Guid` | set by `Create` to `Guid.CreateVersion7()` |
| `FullName` | `string` (not null) | trimmed, 1..200 chars |
| `Email` | `string` (not null) | trimmed, `ToLowerInvariant()`, 1..254 chars (see [ADR 0005](../../docs/adr/0005-email-uniqueness-lowercase.md)) |
| `IsActive` | `bool` | `true` from `Create`; `false` after `Deactivate()` (BR3) |

- `public static Employee Create(string fullName, string email)`
  1. `ArgumentException.ThrowIfNullOrWhiteSpace(fullName)` → `ArgumentNullException` for null,
     `ArgumentException` for empty/whitespace.
  2. `fullName.Trim().Length > FullNameMaxLength` → `new ArgumentException("Full name cannot be longer than 200 characters.", nameof(fullName))`.
  3. `ArgumentException.ThrowIfNullOrWhiteSpace(email)`.
  4. normalised = `email.Trim().ToLowerInvariant()`; `normalised.Length > EmailMaxLength` →
     `new ArgumentException("Email cannot be longer than 254 characters.", nameof(email))`.
  5. Returns an active employee with a new `Id`. No e-mail format check (not in the brief; no use case creates
     employees: they come from the seed).
- `public void Deactivate()`: sets `IsActive = false`. Idempotent: no exception if already inactive. There is no
  `Activate()` (no use case needs it).

### 3.2 `TaskItem` (`public sealed class TaskItem`)

| Member | Type | Notes |
|---|---|---|
| `public const int TitleMaxLength = 200;` | `int` | shared with B1 |
| `Id` | `Guid` | set by `Create` to `Guid.CreateVersion7()` |
| `Title` | `string` (not null) | trimmed, 1..200 chars |
| `Status` | `TaskItemStatus` | `New` after `Create` |
| `CreatorId` | `Guid` | from `creator.Id`; never changes |
| `AssigneeId` | `Guid` | from `assignee.Id`; never changes (no reassignment, [ADR 0002](../../docs/adr/0002-assignee-column-on-task.md)) |
| `PlannedStartAt` | `DateTimeOffset?` | UTC (offset 0) or null |
| `DueAt` | `DateTimeOffset?` | UTC (offset 0) or null; the deadline |
| `CompletedAt` | `DateTimeOffset?` | UTC; non-null iff `Status == Completed` (BR1) |
| `Version` | `uint` | concurrency token, mapped by B1 to PostgreSQL `xmin`; the domain never writes it |

- `public static TaskItem Create(string title, Employee creator, Employee assignee, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt)`

  Guards, **in this order** (A2 relies on the order only where a test has one violation, but pin it anyway):
  1. `ArgumentException.ThrowIfNullOrWhiteSpace(title)`.
  2. `title.Trim().Length > TitleMaxLength` → `new ArgumentException("Title cannot be longer than 200 characters.", nameof(title))`.
  3. `ArgumentNullException.ThrowIfNull(creator)`; `ArgumentNullException.ThrowIfNull(assignee)`.
  4. Normalise both dates with `ToUniversalTime()` (null stays null). See §6.
  5. **BR5**: `creator.Id == assignee.Id` → `new BusinessRuleViolationException("BR5", "BR5: A task cannot be assigned to its creator.")`.
  6. **BR3**: `!assignee.IsActive` → `new BusinessRuleViolationException("BR3", $"BR3: Employee {assignee.Id} is inactive and cannot be given a new task.")`.
  7. **BR2**: both dates non-null and `dueAt < plannedStartAt` → `new BusinessRuleViolationException("BR2", "BR2: DueAt cannot be earlier than PlannedStartAt.")`.
  8. Returns a task with `Status = New`, `CompletedAt = null`, trimmed title, the two ids and the UTC dates.

  The creator's `IsActive` is **not** checked: the brief restricts only who is *given* a task.

- `public void ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt)`

  1. `!Enum.IsDefined(newStatus)` → `new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Unknown task status.")`.
  2. **BR4**: current `Status` is `Completed` or `Cancelled` → `new BusinessRuleViolationException("BR4", $"BR4: Task {Id} is {Status}; Completed and Cancelled are final.")`.
     Nothing changes.
  3. `newStatus == Status` → return (no-op; only reachable for `New` and `InProgress`).
  4. `Status = newStatus; CompletedAt = newStatus == TaskItemStatus.Completed ? changedAt.ToUniversalTime() : null;`
     (**BR1**, structural: this line is the BR1 guard).

### 3.3 `TaskItemStatus` (`public enum TaskItemStatus`)

```
New = 0, InProgress = 1, Completed = 2, Cancelled = 3
```

Spelling is exact (`Cancelled`, double L), because the stored text and the `ck_tasks_status_valid` CHECK use the
member names. See §5.

### 3.4 `BusinessRuleViolationException`

```
public sealed class BusinessRuleViolationException : InvalidOperationException
    public BusinessRuleViolationException(string ruleId, string message) : base(message)
    public BusinessRuleViolationException(string ruleId, string message, Exception innerException) : base(message, innerException)
    public string RuleId { get; }      // "BR2" | "BR3" | "BR4" | "BR5"
```

The three-argument constructor exists for one caller: `TaskService.CreateTaskAsync` uses it to turn the database
trigger's BR3 rejection into this exception, keeping the original as `InnerException` (§14; grill decision Q4).

The only custom exception. Thrown for BR2–BR5 (BR1 cannot be violated through the API). Input errors
(null/blank/too long) stay BCL `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException`;
missing rows are `KeyNotFoundException` (service only). Tests assert `RuleId`, never the message text.
Why it earns its place: [ADR 0008](../../docs/adr/0008-business-rule-violation-exception.md).

## 4. Assignment model

Assignee is a non-null column `assignee_id` on `tasks`, set once by `TaskItem.Create`. No `TaskAssignment` table,
no assign/reassign operation. BR5 then fits a row-local CHECK (`assignee_id <> creator_id`); a history table would
put the assignee on another row, which a PostgreSQL CHECK cannot see (no subqueries). Full reasoning:
[ADR 0002](../../docs/adr/0002-assignee-column-on-task.md).

## 5. Statuses

- Stored as text: B1 `.HasConversion<string>().HasMaxLength(20)` → column `status character varying(20) NOT NULL`,
  values `'New' | 'InProgress' | 'Completed' | 'Cancelled'`, restricted by `ck_tasks_status_valid`.
  ADR: [0007](../../docs/adr/0007-status-as-text-and-transition-rule.md).
- **Transition rule**: a change is allowed iff the current status is not final. Changing to the same non-final status
  is an allowed no-op. Nothing else is restricted, because the brief defines only BR4.

Full table (`ChangeStatus(to, changedAt)` called on a task in status `from`):

| from | to | allowed | result | `CompletedAt` after |
|---|---|---|---|---|
| New | New | yes | no-op | null |
| New | InProgress | yes | status = InProgress | null |
| New | Completed | yes | status = Completed | `changedAt` (UTC) |
| New | Cancelled | yes | status = Cancelled | null |
| InProgress | New | yes | status = New | null |
| InProgress | InProgress | yes | no-op | null |
| InProgress | Completed | yes | status = Completed | `changedAt` (UTC) |
| InProgress | Cancelled | yes | status = Cancelled | null |
| Completed | New | no | BR4 exception, unchanged | unchanged (non-null) |
| Completed | InProgress | no | BR4 exception, unchanged | unchanged (non-null) |
| Completed | Completed | no | BR4 exception, unchanged | unchanged (non-null) |
| Completed | Cancelled | no | BR4 exception, unchanged | unchanged (non-null) |
| Cancelled | New | no | BR4 exception, unchanged | null |
| Cancelled | InProgress | no | BR4 exception, unchanged | null |
| Cancelled | Completed | no | BR4 exception, unchanged | null |
| Cancelled | Cancelled | no | BR4 exception, unchanged | null |

BR4 finality shows up in three places: the domain guard (step 2 above), the service (which only calls the domain),
and the `xmin` concurrency token, which stops two concurrent changes from both loading a non-final task and the
second silently overwriting the first's final status (§10). At the database level, the trigger `trg_tasks_br3_br4`
rejects any status change out of a final status for every writer, raw SQL included (§11,
[ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)).

## 6. Time

ADR: [0006](../../docs/adr/0006-utc-normalisation-and-explicit-time.md).

- All timestamps are `DateTimeOffset`, stored as `timestamp with time zone` (EF/Npgsql default mapping, seen in the
  N0 probe's migration).
- **The domain normalises, it does not reject**: every `DateTimeOffset` entering `TaskItem` goes through
  `ToUniversalTime()`, so stored values always have `Offset == TimeSpan.Zero` and the same instant. Reason: Npgsql
  10.0.3 throws on write for any non-zero offset (DLL string: "Cannot write DateTimeOffset with Offset=… to
  PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported."; npgsql.org datetime page:
  "only with Offset=0"). BR2 compares instants, so normalising never changes a BR2 outcome.
- **"Now"**: the domain never reads a clock. `ChangeStatus` takes `changedAt` explicitly. `TaskService` gets
  `TimeProvider` (BCL, `System.TimeProvider`) by constructor and passes `timeProvider.GetUtcNow()`. Tests pass a
  fixed `DateTimeOffset` (A2) or a `TimeProvider` subclass that overrides `GetUtcNow()` (B3; the method is
  `virtual`, checked by reflection). Use whole-second times in B3: `timestamptz` keeps microseconds, .NET keeps
  100 ns ticks.
- Reading: Npgsql returns `timestamptz` as `DateTimeOffset` with offset 0. `DateTimeOffset` equality compares
  instants, so assertions do not depend on offsets anyway.

## 7. Deadlines and BR2

`PlannedStartAt` and `DueAt` are both nullable (a task may have no plan and/or no deadline). BR2 applies only when
both are set: `DueAt >= PlannedStartAt` (equal is allowed; "can't be earlier"). With either null, BR2 is satisfied.
Same rule in the domain (§3.2 step 7) and the DB (`ck_tasks_br2_due_at_not_before_planned_start_at`). See
[[br2-due-not-before-start]].

## 8. BR3: "given a new task"

An employee is *given a new task* when a `TaskItem` is created with that employee as `AssigneeId`. Creation is the
only moment `AssigneeId` is set (no reassignment), so BR3 is checked exactly there, three times:

1. **Application** (`TaskService.CreateTaskAsync`): after loading the assignee row from the database, before
   calling the domain, `!assignee.IsActive` → `BusinessRuleViolationException("BR3", …)`. This is the check against
   the persisted state.
2. **Domain** (`TaskItem.Create` step 6): the same check on the `Employee` object it is handed, so no caller can
   skip it.
3. **Database** (trigger `trg_tasks_br3_br4`, §11): on every insert into `tasks`, the assignee row is read under
   `FOR SHARE`. A deactivation committed between the service's read and its insert is therefore caught: the insert
   fails with `23514` / `trg_tasks_br3_assignee_active`, and the service rethrows it as
   `BusinessRuleViolationException("BR3", …, inner)` (§14). While the insert's transaction is open, a concurrent
   deactivation of that employee waits for it. [ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md).

Not covered, by design: tasks an employee already has when deactivated stay valid and can still change status
(BR3 restricts *new* tasks only). See [[br3-inactive-assignee]].

## 9. Keys

`Guid` primary keys generated by the domain with `Guid.CreateVersion7()` (.NET 9+ BCL, present in the 10.0.12 ref
pack), mapped `ValueGeneratedNever()`, column type `uuid`. There is no identity column and no sequence, so `HasData`
seeding with fixed Guids cannot collide with application inserts. ADR:
[0003](../../docs/adr/0003-domain-generated-uuid-keys.md).

## 10. Relational mapping (B1)

Every property gets an explicit `HasColumnName` (the N0 probe showed that unconfigured properties keep PascalCase
names such as `Id`, `CreatedAt`, and the PK defaults to `PK_employees`). No naming-convention package.

### `employees` (`EmployeeConfiguration : IEntityTypeConfiguration<Employee>`)

| Property | Column | Store type | Null | Config |
|---|---|---|---|---|
| `Id` | `id` | `uuid` | no | `HasKey(e => e.Id).HasName("pk_employees")`; `ValueGeneratedNever()` |
| `FullName` | `full_name` | `character varying(200)` | no | `HasMaxLength(Employee.FullNameMaxLength)`, `IsRequired()` |
| `Email` | `email` | `character varying(254)` | no | `HasMaxLength(Employee.EmailMaxLength)`, `IsRequired()` |
| `IsActive` | `is_active` | `boolean` | no | |

`builder.ToTable("employees")`. No concurrency token on `Employee`: its only mutation (`Deactivate`) is idempotent
and no use case calls it, so there is no lost update to prevent.

### `tasks` (`TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>`)

| Property | Column | Store type | Null | Config |
|---|---|---|---|---|
| `Id` | `id` | `uuid` | no | `HasKey(t => t.Id).HasName("pk_tasks")`; `ValueGeneratedNever()` |
| `Title` | `title` | `character varying(200)` | no | `HasMaxLength(TaskItem.TitleMaxLength)`, `IsRequired()` |
| `Status` | `status` | `character varying(20)` | no | `HasConversion<string>()`, `HasMaxLength(20)` (Learn, value conversions: facets "apply to the converted database type"; B1 confirms the store type in `Up()`) |
| `CreatorId` | `creator_id` | `uuid` | no | FK |
| `AssigneeId` | `assignee_id` | `uuid` | no | FK |
| `PlannedStartAt` | `planned_start_at` | `timestamp with time zone` | yes | |
| `DueAt` | `due_at` | `timestamp with time zone` | yes | |
| `CompletedAt` | `completed_at` | `timestamp with time zone` | yes | |
| `Version` | `xmin` (system column) | `xid` | no | `Property(t => t.Version).IsRowVersion()`; no `HasColumnName` (Npgsql's convention maps a `uint` row version to `xmin`) |

- **Concurrency token**: CLR property `TaskItem.Version`, on `TaskItem` only. This is the exact shape the N0 probe
  proved: the migration lists `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)`, Npgsql does
  not create it (system column), and `HasData` leaves it out of `InsertData`. A shadow property would also match
  Npgsql's convention (`ProcessRowVersionProperty` works on any `uint` concurrency-token property), but it was not
  probed, so the spec takes the proven path.
- **Foreign keys**, both `DeleteBehavior.Restrict` (→ `ON DELETE RESTRICT`):
  - `HasOne<Employee>().WithMany().HasForeignKey(t => t.CreatorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tasks_employees_creator_id")`
  - `HasOne<Employee>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_tasks_employees_assignee_id")`
  - Why Restrict: employees are deactivated, not deleted. Cascade would silently delete task history; SetNull is
    impossible on non-null columns. Deleting an employee who still has tasks must fail loudly.
- The model has no navigations, so B1 does not touch `Employee` for the relationships.

## 11. Indexes and constraints (complete list)

The migration must contain exactly these, no more (B1 checks the generated `Up()`; if EF adds an unnamed
`IX_tasks_assignee_id` for the FK despite the composite index covering it, B1 reports it `[uncertain]` rather than
leaving a default-named index).

| Name | Table | Kind | Columns / SQL (exact) | Enforces |
|---|---|---|---|---|
| `pk_employees` | employees | PK | `(id)` | identity |
| `pk_tasks` | tasks | PK | `(id)` | identity |
| `ux_employees_email` | employees | unique index | `(email)` | unique e-mail (case-insensitive via lowercase normalisation, [ADR 0005](../../docs/adr/0005-email-uniqueness-lowercase.md)) |
| `ix_tasks_assignee_id_status` | tasks | index | `(assignee_id, status)` | brief: assignee + status; serves "list by assignee" via its leading column |
| `ix_tasks_due_at` | tasks | index | `(due_at)` | brief: due date |
| `ix_tasks_creator_id` | tasks | index | `(creator_id)` | FK lookups for `ON DELETE RESTRICT` (EF would create it anyway; named here so it is not `IX_…`) |
| `fk_tasks_employees_creator_id` | tasks | FK | `(creator_id) → employees(id) ON DELETE RESTRICT` | referential integrity |
| `fk_tasks_employees_assignee_id` | tasks | FK | `(assignee_id) → employees(id) ON DELETE RESTRICT` | referential integrity |
| `ck_tasks_status_valid` | tasks | CHECK | `status IN ('New', 'InProgress', 'Completed', 'Cancelled')` | enum values |
| `ck_tasks_br1_completed_at_iff_completed` | tasks | CHECK | `(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)` | **BR1** |
| `ck_tasks_br2_due_at_not_before_planned_start_at` | tasks | CHECK | `planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at` | **BR2** |
| `ck_tasks_br5_assignee_not_creator` | tasks | CHECK | `assignee_id <> creator_id` | **BR5** |
| `trg_tasks_br3_br4` (function `tasks_enforce_br3_br4()`) | tasks | trigger, `BEFORE INSERT OR UPDATE OF status`, `FOR EACH ROW` | SQL below; its errors name `trg_tasks_br3_assignee_active` and `trg_tasks_br4_final_status` | **BR3**, **BR4** |

Fluent calls (B1):

```
builder.ToTable("tasks", t =>
{
    t.HasCheckConstraint("ck_tasks_status_valid", "status IN ('New', 'InProgress', 'Completed', 'Cancelled')");
    t.HasCheckConstraint("ck_tasks_br1_completed_at_iff_completed", "(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)");
    t.HasCheckConstraint("ck_tasks_br2_due_at_not_before_planned_start_at", "planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at");
    t.HasCheckConstraint("ck_tasks_br5_assignee_not_creator", "assignee_id <> creator_id");
});
builder.HasIndex(t => new { t.AssigneeId, t.Status }).HasDatabaseName("ix_tasks_assignee_id_status");
builder.HasIndex(t => t.DueAt).HasDatabaseName("ix_tasks_due_at");
builder.HasIndex(t => t.CreatorId).HasDatabaseName("ix_tasks_creator_id");
// employees:
builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName("ux_employees_email");
```

All DDL above (tables, every constraint, every index, the seed) was run verbatim in a throwaway `postgres:17-alpine`
(PostgreSQL 17.10) container; each violation below produced exactly the SqlState and constraint name listed in §15.

**BR3 and BR4 are enforced by a trigger**, because a row-local CHECK sees neither another row (the assignee's
`is_active`, needed for BR3) nor the row's previous value (the old status, needed for BR4). This follows the
pre-implementation grill decision Q1(b); see [ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md), which
supersedes ADR 0004. Both errors use SqlState `23514` (`check_violation`) and carry a constraint name, so tests
assert them exactly like the CHECKs. The exact SQL, verified in a throwaway `postgres:17-alpine` container:

```sql
CREATE FUNCTION tasks_enforce_br3_br4() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    assignee_active boolean;
BEGIN
    IF TG_OP = 'INSERT' THEN
        -- BR3: the assignee must be active. FOR SHARE blocks a concurrent deactivation until this transaction ends.
        -- An unknown assignee leaves assignee_active NULL, so the FK reports it (23503), not BR3.
        SELECT is_active INTO assignee_active FROM employees WHERE id = NEW.assignee_id FOR SHARE;
        IF assignee_active IS FALSE THEN
            RAISE EXCEPTION 'BR3: employee % is inactive and cannot be given a new task.', NEW.assignee_id
                USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br3_assignee_active';
        END IF;
    ELSIF OLD.status IN ('Completed', 'Cancelled') AND NEW.status IS DISTINCT FROM OLD.status THEN
        -- BR4: Completed and Cancelled are final.
        RAISE EXCEPTION 'BR4: task % is %; Completed and Cancelled are final.', OLD.id, OLD.status
            USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br4_final_status';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_tasks_br3_br4
    BEFORE INSERT OR UPDATE OF status ON tasks
    FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4();
```

In that container: an active assignee was accepted. An inactive assignee got `23514` / `trg_tasks_br3_assignee_active`.
An unknown assignee got `23503` / `fk_tasks_employees_assignee_id` (the FK, not BR3). A status change on a `Completed`
task got `23514` / `trg_tasks_br4_final_status`. An update of another column on a `Completed` task, or setting the
same status again, was accepted. The drops below succeeded.

**How B1 adds it.** EF Core does not create triggers from the model. B1 edits the generated `InitialCreate`:
- At the **end** of `Up()`, after the tables, indexes and seed: `migrationBuilder.Sql(...)` with the function,
  then with the trigger (`MigrationBuilder.Sql(string, bool)`, Relational 10.0.12).
- At the **start** of `Down()`: `migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_tasks_br3_br4 ON tasks;")` and
  `migrationBuilder.Sql("DROP FUNCTION IF EXISTS tasks_enforce_br3_br4();")`.
- The migration's header comment lists the function and the trigger with the rest of the schema changes.
- `TaskItemConfiguration`'s XML docs note that the trigger lives in the migration, not in the model.
- Measured cost (throwaway container, spec schema, 1,000 employees): +21 µs per single-row insert, +4 µs per
  status update, nothing on reads; bulk inserts about 60% slower. Details: ADR 0009.

## 12. Seed data (B1, `HasData` in each configuration class)

Fixed ids, fixed UTC timestamps (`new DateTimeOffset(y, M, d, h, m, s, TimeSpan.Zero)`). Anonymous objects keyed by
CLR property names (the N0 probe used this form). `Version` is not set.

`employees`:

| `Id` | `FullName` | `Email` | `IsActive` |
|---|---|---|---|
| `10000000-0000-0000-0000-000000000001` | `Alice Morgan` | `alice.morgan@example.com` | `true` |
| `10000000-0000-0000-0000-000000000002` | `Bob Chen` | `bob.chen@example.com` | `true` |
| `10000000-0000-0000-0000-000000000003` | `Carol Diaz` | `carol.diaz@example.com` | `false` |

`tasks`:

| `Id` | `Title` | `Status` | `CreatorId` | `AssigneeId` | `PlannedStartAt` | `DueAt` | `CompletedAt` |
|---|---|---|---|---|---|---|---|
| `20000000-0000-0000-0000-000000000001` | `Prepare Q4 sales report` | `New` | Alice `…0001` | Bob `…0002` | 2026-10-01 09:00:00Z | 2026-10-10 17:00:00Z | null |
| `20000000-0000-0000-0000-000000000002` | `Call back key account` | `Completed` | Bob `…0002` | Alice `…0001` | 2026-09-01 09:00:00Z | 2026-09-05 17:00:00Z | 2026-09-04 15:30:00Z |
| `20000000-0000-0000-0000-000000000003` | `Clean up duplicate contacts` | `Cancelled` | Alice `…0001` | Bob `…0002` | null | null | null |

Checks: BR1 (only the Completed row has `CompletedAt`), BR2 (due ≥ planned or null), BR5 (creator ≠ assignee on
every row), BR3 (no task assigned to Carol, the inactive employee), status values valid, e-mails lowercase and
unique. Carol exists so BR3 can be tested against the database.

The seed is **demo data** that lives in `InitialCreate` (grill decision Q3(a)), so every database the migration
runs against gets these rows. D1's README states this in a `## Seed data` section.

B1 confirms two things in the generated `Up()`, both `[uncertain]` until then (the N0 probe seeded one table with
no value converter): the `InsertData` for `tasks` carries the status as the strings `"New"`, `"Completed"`,
`"Cancelled"` (not integers, which `ck_tasks_status_valid` would reject), and the `employees` inserts come before the
`tasks` inserts (FK order).

## 13. DbContext contract (B1)

- `public sealed class TaskManagementDbContext : DbContext`, namespace `TaskManagement.Infrastructure`.
- Constructor: `TaskManagementDbContext(DbContextOptions<TaskManagementDbContext> options)` (primary or explicit,
  B1's choice).
- `public DbSet<Employee> Employees => Set<Employee>();`
- `public DbSet<TaskItem> Tasks => Set<TaskItem>();`
- `OnModelCreating`: `modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskManagementDbContext).Assembly);`
- Configurations: namespace `TaskManagement.Infrastructure.Configurations`, `public sealed class EmployeeConfiguration`
  and `public sealed class TaskItemConfiguration`.
- Migration: name `InitialCreate`, namespace `TaskManagement.Infrastructure.Migrations`, one migration only.
- **Design time**: `public sealed class TaskManagementDbContextFactory : IDesignTimeDbContextFactory<TaskManagementDbContext>`
  (namespace `TaskManagement.Infrastructure`). `CreateDbContext(string[] args)` returns a context built with
  `UseNpgsql("Host=localhost;Port=5432;Database=task_management;Username=postgres")`. That string holds no secret
  and is only a placeholder for model building: `migrations add` and `migrations list --no-connect` never connect.
  Anything that touches a real database passes `--connection "<connection string>"` (dotnet-ef 10.0.12 `--help`:
  "The connection string to the database."). The N0 probe used this exact pattern on a class library.
- Commands (from the repo root):

  ```
  dotnet ef migrations add InitialCreate --project src/TaskManagement.Infrastructure
  dotnet ef migrations list --project src/TaskManagement.Infrastructure --no-connect
  dotnet ef database update --project src/TaskManagement.Infrastructure --connection "<connection string>"
  ```

  These match `AGENTS.md`. From the repo root (no project file there), dotnet-ef falls back to `--project` as the
  startup project: verified by orch on the N0 probe, where the command exited 0 with and without `--startup-project`.

## 14. Application service contract (B2)

- `public sealed class TaskService`, namespace `TaskManagement.Application`.
- Constructor: `TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider)`. No interface: one
  implementation, no mocking need (integration tests use a real database).
- Methods:

```
public Task<TaskItem> CreateTaskAsync(string title, Guid creatorId, Guid assigneeId, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt, CancellationToken cancellationToken = default)
public Task<TaskItem> ChangeTaskStatusAsync(Guid taskId, TaskItemStatus newStatus, CancellationToken cancellationToken = default)
public Task<IReadOnlyList<TaskItem>> ListTasksByAssigneeAsync(Guid assigneeId, TaskItemStatus? status = null, CancellationToken cancellationToken = default)
```

(`async` is an implementation detail; the signatures above are what callers see.)

**`CreateTaskAsync`**
1. `creator = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == creatorId, cancellationToken)`; null →
   `new KeyNotFoundException($"Employee {creatorId} was not found.")`.
2. Same for `assigneeId` (same message shape).
3. **BR3 (service check)**: `!assignee.IsActive` → `new BusinessRuleViolationException("BR3", $"BR3: Employee {assigneeId} is inactive and cannot be given a new task.")`.
4. `task = TaskItem.Create(title, creator, assignee, plannedStartAt, dueAt)` (domain re-checks BR5, BR3, BR2).
5. `dbContext.Tasks.Add(task); await dbContext.SaveChangesAsync(cancellationToken);` return `task`.
6. **BR3 race (grill Q4)**: wrap only that `SaveChangesAsync` call in
   `catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.CheckViolation && pg.ConstraintName == "trg_tasks_br3_assignee_active")`
   and throw `new BusinessRuleViolationException("BR3", $"BR3: Employee {assigneeId} is inactive and cannot be given a new task.", ex)`.
   Every other `DbUpdateException` propagates unchanged. `PostgresException` and `PostgresErrorCodes` live in
   namespace `Npgsql` (package `Npgsql` 10.0.3, which reaches Application transitively through Infrastructure).
   The equality form (not a property pattern) is deliberate: it compiles whether `CheckViolation` is a `const` or a
   `static readonly` field.
7. **Failed-save cleanup (D4, D3 findings F1/SF1)**: if `SaveChangesAsync` does not complete (any exception,
   translated or not), set `dbContext.Entry(task).State = EntityState.Detached` in a `finally`, so the caller's
   next save on the same context does not send the rejected insert again.

**`ChangeTaskStatusAsync`**
1. `task = await dbContext.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)`; null →
   `new KeyNotFoundException($"Task {taskId} was not found.")`.
2. `task.ChangeStatus(newStatus, timeProvider.GetUtcNow())` (BR4, BR1).
3. `await dbContext.SaveChangesAsync(cancellationToken)`; a `DbUpdateConcurrencyException` (another writer changed
   the row since it was read) is **not caught**: it propagates and is listed in the XML `<exception>` docs. If the
   save does not complete, detach the task in a `finally` (D4, D3 finding SF2), so a retry on the same context
   reloads the current row instead of starting from the failed change.
4. Return `task`.

**`ListTasksByAssigneeAsync`**
- `status` has a value that is not a defined member (`!Enum.IsDefined(status.Value)`) →
  `new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status.")`.
- Query: `dbContext.Tasks.AsNoTracking().Where(t => t.AssigneeId == assigneeId)`, then `.Where(t => t.Status == status.Value)`
  only when `status` has a value, then `.OrderBy(t => t.DueAt).ThenBy(t => t.Id).ToListAsync(cancellationToken)`.
  With a status, both columns of `ix_tasks_assignee_id_status` serve the filter (grill Q2); without one, its leading
  column does.
- The returned tasks are untracked: calling `ChangeStatus` on one changes nothing in the database. The XML docs say
  so; status changes go through `ChangeTaskStatusAsync`.
- Unknown or task-less assignee → empty list, no exception. Tasks with no `DueAt` come last (PostgreSQL's default
  for `ASC` is `NULLS LAST`, confirmed in the container). `[uncertain]` that EF/Npgsql emits a plain
  `ORDER BY due_at, id` with no null-ordering rewrite; B3's ordering test settles it at FB.

Exceptions the service lets through, all documented with `<exception>`: `KeyNotFoundException`,
`BusinessRuleViolationException`, `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException` (from
the domain or the status filter), `DbUpdateConcurrencyException`, any other `DbUpdateException`,
`OperationCanceledException`. `ChangeTaskStatusAsync` needs no BR4 translation: an EF update carries the `xmin`
token, so a row finalised by another writer fails the concurrency check before the BR4 trigger could fire.

## 15. Test plan

Test names follow `Method_Scenario_Expected`. Every test method carries its trait.

### A2 unit tests (`tests/TaskManagement.UnitTests`, namespace `TaskManagement.UnitTests`, `[Trait("Category", "Unit")]`)

Fixed times: `static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero)` and offsets from it.

`EmployeeTests`
| Test | Checks |
|---|---|
| `Create_ValidInput_SetsPropertiesAndIsActive` | FullName, Email, `IsActive == true`, `Id != Guid.Empty` |
| `Create_MixedCaseEmailWithSpaces_StoresTrimmedLowercase` | `"  Alice.Morgan@Example.COM "` → `"alice.morgan@example.com"` |
| `Create_NullFullName_ThrowsArgumentNullException` | |
| `Create_BlankFullName_ThrowsArgumentException` | Theory `""`, `"   "`; `Assert.Throws<ArgumentException>` (exact type) |
| `Create_FullNameOf200Chars_Succeeds` / `Create_FullNameOf201Chars_ThrowsArgumentException` | boundary |
| `Create_NullEmail_ThrowsArgumentNullException` | |
| `Create_BlankEmail_ThrowsArgumentException` | Theory `""`, `"   "` |
| `Create_EmailOf254Chars_Succeeds` / `Create_EmailOf255Chars_ThrowsArgumentException` | `new string('a', 242) + "@example.com"` / `243` |
| `Create_TwoCalls_ReturnDistinctIds` | |
| `Deactivate_ActiveEmployee_SetsIsActiveFalse` | BR3 precondition |
| `Deactivate_InactiveEmployee_StaysInactive` | idempotent, no throw |

`TaskItemCreateTests`
| Test | Rule |
|---|---|
| `Create_ValidInput_StartsAsNewWithNullCompletedAt` | BR1 |
| `Create_ValidInput_CopiesTitleIdsAndDates` | |
| `Create_TitleWithSurroundingSpaces_StoresTrimmedTitle` | |
| `Create_NullTitle_ThrowsArgumentNullException` | |
| `Create_BlankTitle_ThrowsArgumentException` (Theory `""`, `"   "`) | |
| `Create_TitleOf201Chars_ThrowsArgumentException` | |
| `Create_TitleOf200Chars_Succeeds` | boundary, accepted side (D4, D3 finding J-11) |
| `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` | guard order: BR5 before BR3 (D4, D3 finding J-5); the service-half BR3 test relies on it |
| `Create_NullCreator_ThrowsArgumentNullException` / `Create_NullAssignee_ThrowsArgumentNullException` | |
| `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5` | BR5 (same `Employee` passed twice; assert `RuleId == "BR5"`) |
| `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3` | BR3 (`Deactivate()` then create) |
| `Create_InactiveCreatorActiveAssignee_Succeeds` | BR3 scope |
| `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2` | BR2 (planned `T0`, due `T0.AddHours(-1)`) |
| `Create_DueAtEqualsPlannedStartAt_Succeeds` | BR2 boundary |
| `Create_PlannedStartAtOrDueAtMissing_Succeeds` | BR2 with nulls; Theory `(bool hasPlanned, bool hasDue)`: `(true,false)`, `(false,true)`, `(false,false)` |
| `Create_NonUtcOffsets_StoresSameInstantWithZeroOffset` | time (§6) |
| `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2` | BR2 + time: planned `2026-10-01T10:00+00:00`, due `2026-10-01T11:00+02:00` (= 09:00Z) |

`TaskItemChangeStatusTests`
| Test | Rule |
|---|---|
| `ChangeStatus_TransitionTableRow_BehavesAsSpecified` | BR4 + BR1. Theory with 16 `[InlineData(from, to, allowed)]` rows, exactly the §5 table. Arrange: new task, then reach `from` via `ChangeStatus(from, T0)` (skip for `New`). Allowed rows: `Status == to` and `CompletedAt == (to == Completed ? changedAt : null)`. Disallowed rows: `BusinessRuleViolationException` with `RuleId == "BR4"`, and `Status`/`CompletedAt` unchanged. |
| `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt` | BR1 |
| `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant` | BR1 + time |
| `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt` | BR4 + BR1 |
| `ChangeStatus_UndefinedStatus_ThrowsArgumentOutOfRangeException` | `(TaskItemStatus)99` |

### B3 integration tests (`tests/TaskManagement.IntegrationTests`, namespace `TaskManagement.IntegrationTests`, `[Trait("Category", "Integration")]`)

**Fixture** (B3's own shape; recommended): `PostgresFixture : IAsyncLifetime` starts one container,
`new PostgreSqlBuilder("postgres:17-alpine").Build()`, shared through
`[CollectionDefinition("Postgres")] public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>`
(same file). Every test gets its **own fresh database**: the fixture copies `GetConnectionString()` into an
`NpgsqlConnectionStringBuilder` with a unique `Database` name and calls `Database.MigrateAsync()`, which "will create
the database if it does not already exist" (EF XML docs). So seed rows are always pristine and tests are independent.
`[uncertain]` whether CA1001 fires on a fixture that holds the container (`IAsyncDisposable`) but implements only
xUnit's `IAsyncLifetime`; B3 resolves it if the build says so.

Raw SQL runs through `db.Database.ExecuteSqlRawAsync(sql)` and is asserted with
`var ex = await Assert.ThrowsAsync<PostgresException>(...)`, then `ex.SqlState` and `ex.ConstraintName`.
`[uncertain]` that `ExecuteSqlRawAsync` surfaces `PostgresException` unwrapped (EF wraps only `SaveChanges`
failures in `DbUpdateException`); if not, assert on the inner exception.

`MigrationAndSeedTests`
| Test | Checks |
|---|---|
| `Migrate_EmptyDatabase_AppliesAllMigrationsAndLeavesNonePending` | `GetPendingMigrationsAsync()` empty; `GetAppliedMigrationsAsync()` equals `GetMigrations()` and is non-empty |
| `Schema_AfterMigration_HasSpecifiedConstraintsAndIndexes` | `SELECT conname AS "Value" FROM pg_constraint WHERE conrelid IN ('employees'::regclass, 'tasks'::regclass)` and `SELECT indexname AS "Value" FROM pg_indexes WHERE tablename IN ('employees', 'tasks')` via `Database.SqlQueryRaw<string>` contain every name in §11, and `SELECT tgname AS "Value" FROM pg_trigger WHERE tgrelid = 'tasks'::regclass AND NOT tgisinternal` returns exactly `trg_tasks_br3_br4` |
| `Seed_Employees_MatchSpec` | the 3 rows of §12, every column |
| `Seed_Tasks_MatchSpec` | the 3 rows of §12, every column |

`DatabaseConstraintTests` (ids `30000000-…` are new rows; employee ids from §12)
| Test | SQL (exact) | Expected SqlState / constraint |
|---|---|---|
| `Insert_CompletedWithoutCompletedAt_RejectedByBR1Check` | `INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES ('30000000-0000-0000-0000-000000000001', 'BR1 violation', 'Completed', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', NULL, NULL, NULL)` | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Insert_NewWithCompletedAt_RejectedByBR1Check` | same columns, `'30000000-0000-0000-0000-000000000002', 'BR1 violation', 'New', …0001, …0002, NULL, NULL, '2026-09-18T12:00:00+00:00'` | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check` | `UPDATE tasks SET completed_at = NULL WHERE id = '20000000-0000-0000-0000-000000000002'` | `23514` / `ck_tasks_br1_completed_at_iff_completed` |
| `Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check` | `'30000000-0000-0000-0000-000000000004', 'BR2 violation', 'New', …0001, …0002, '2026-10-10T09:00:00+00:00', '2026-10-01T09:00:00+00:00', NULL` | `23514` / `ck_tasks_br2_due_at_not_before_planned_start_at` |
| `Insert_DueAtEqualsPlannedStartAt_Accepted` | `'30000000-0000-0000-0000-000000000005', 'BR2 boundary', 'New', …0001, …0002, '2026-10-01T09:00:00+00:00', '2026-10-01T09:00:00+00:00', NULL` | no exception, 1 row |
| `Insert_AssigneeEqualsCreator_RejectedByBR5Check` | `'30000000-0000-0000-0000-000000000006', 'BR5 violation', 'New', …0001, …0001, NULL, NULL, NULL` | `23514` / `ck_tasks_br5_assignee_not_creator` |
| `Insert_UnknownStatus_RejectedByStatusCheck` | `'30000000-0000-0000-0000-000000000007', 'Bad status', 'Done', …0001, …0002, NULL, NULL, NULL` | `23514` / `ck_tasks_status_valid` |
| `Insert_DuplicateEmail_RejectedByUniqueIndex` | `INSERT INTO employees (id, full_name, email, is_active) VALUES ('10000000-0000-0000-0000-000000000009', 'Alice Duplicate', 'alice.morgan@example.com', true)` | `23505` / `ux_employees_email` |
| `Delete_EmployeeWithTasks_RejectedByRestrictForeignKey` | `DELETE FROM employees WHERE id = '10000000-0000-0000-0000-000000000002'` | `23503`; constraint is `fk_tasks_employees_creator_id` or `fk_tasks_employees_assignee_id` (Bob is referenced by both; the container reported `…creator_id`) |
| `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` | EF, not raw SQL: two separate `TaskManagementDbContext` instances on **this test's** database both load task `20000000-0000-0000-0000-000000000001`; context 1 `ChangeStatus(Completed, t)` + save; context 2 `ChangeStatus(Cancelled, t)` + save | `DbUpdateConcurrencyException` on context 2's save (xmin token, protects BR4) |
| `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger` | `'30000000-0000-0000-0000-000000000008', 'BR3 violation', 'New', …0001, '10000000-0000-0000-0000-000000000003', NULL, NULL, NULL` (assignee Carol, inactive) | `23514` / `trg_tasks_br3_assignee_active` |
| `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger` | `'30000000-0000-0000-0000-000000000009', 'Unknown assignee', 'New', …0001, '10000000-0000-0000-0000-000000000009', NULL, NULL, NULL` | `23503` / `fk_tasks_employees_assignee_id` (proves the trigger does not mask the FK) |
| `Update_StatusOfCompletedTask_RejectedByBR4Trigger` | `UPDATE tasks SET status = 'New', completed_at = NULL WHERE id = '20000000-0000-0000-0000-000000000002'` | `23514` / `trg_tasks_br4_final_status` |
| `Update_StatusOfCancelledTask_RejectedByBR4Trigger` | `UPDATE tasks SET status = 'New' WHERE id = '20000000-0000-0000-0000-000000000003'` | `23514` / `trg_tasks_br4_final_status` (the trigger's `Cancelled` branch; D4, D3 finding J-3) |

(`…0001` = `'10000000-0000-0000-0000-000000000001'`, `…0002` = `'10000000-0000-0000-0000-000000000002'`; the column
list is the one in the first row.)

`TaskServiceTests` (fixed `TimeProvider` returning `2026-09-18T12:00:00Z`; results re-read through a second context)
| Test | Rule |
|---|---|
| `CreateTaskAsync_ValidInput_PersistsNewTask` | use case 1 |
| `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing` | BR3 (service), assignee Carol `…0003` |
| `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` | BR3 (service half): creator = assignee = Carol `…0003`. The service checks BR3 before calling the domain, so `RuleId == "BR3"`. Without the service check, `TaskItem.Create` would throw BR5 first (§3.2 step 5 comes before step 6), so this test goes red when only the service check is removed. It depends on BR5 being checked before BR3 in `TaskItem.Create`: reordering those guards makes this test stop proving the service half. |
| `CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` | BR5 |
| `CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` | BR2 |
| `CreateTaskAsync_UnknownCreator_ThrowsKeyNotFoundException` / `CreateTaskAsync_UnknownAssignee_ThrowsKeyNotFoundException` | not found |
| `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` | use case 2, BR1 (task `…0001`, `CompletedAt == 2026-09-18T12:00:00Z`) |
| `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged` | BR4 (task `20000000-0000-0000-0000-000000000002`) |
| `ChangeTaskStatusAsync_UnknownTask_ThrowsKeyNotFoundException` | not found |
| `ListTasksByAssigneeAsync_Bob_ReturnsHisTasksOrderedByDueAt` | use case 3: arrange a third Bob task `30000000-0000-0000-0000-000000000101` (planned 2026-09-20, due 2026-11-01); expect `[…0001, …0101, …0003]` (null `DueAt` last), an order that differs from ordering by id, planned start or insertion (D4, D3 finding J-1) |
| `ListTasksByAssigneeAsync_EmployeeWithoutTasks_ReturnsEmpty` | Carol |
| `ListTasksByAssigneeAsync_UnknownEmployee_ReturnsEmpty` | |
| `ListTasksByAssigneeAsync_BobFilteredByStatus_ReturnsOnlyMatchingTasks` | use case 3 + status filter (Q2). Theory `(TaskItemStatus status, string expectedTaskId)`: `(New, "20000000-0000-0000-0000-000000000001")`, `(Cancelled, "20000000-0000-0000-0000-000000000003")`; exactly one task returned. Arrange: Alice gets a `New` and a `Cancelled` task first (`…0102`, `…0103`), so a filter that dropped the assignee condition would fail (D4, D3 finding J-2) |
| `ListTasksByAssigneeAsync_UndefinedStatus_ThrowsArgumentOutOfRangeException` | `(TaskItemStatus)99` |
| `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` | BR3 race, DB half + translation (Q4). Arrange on one context `db`: load Bob (`…0002`) with `db.Employees.SingleAsync(...)`, so he is tracked as active. Then `db.Database.ExecuteSqlRawAsync("UPDATE employees SET is_active = false WHERE id = '10000000-0000-0000-0000-000000000002'")`. The tracked Bob stays stale, because EF returns an already-tracked instance without overwriting it (`[uncertain]` until the test runs). Act: `new TaskService(db, time).CreateTaskAsync("Race", Alice, Bob, null, null)`. Assert: `BusinessRuleViolationException` with `RuleId == "BR3"`, `InnerException` is `DbUpdateException` whose `InnerException` is `PostgresException` with `ConstraintName == "trg_tasks_br3_assignee_active"`, and a second context finds no task titled `Race`. Goes red without the trigger (the insert succeeds) or without the translation (`DbUpdateException` escapes). |
| `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` | failed-save cleanup (D4, D3 findings F1/SF1): after the race rejection, `CreateTaskAsync("Next", Bob, Alice)` on the same context succeeds and no `Race` row exists |
| `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds` | failed-save cleanup (D4, D3 finding SF2): another context moves `…0001` to `InProgress`; the stale change to `Completed` fails with `DbUpdateConcurrencyException`; the retry on the same context succeeds, with `CompletedAt` = the fixed time |
| `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException` | a non-check-violation error passes the BR3 filter untranslated and the context stays usable (D4, D3 finding J-4; the filter's constraint-name condition is not reachable through the service): a save interceptor deletes a fresh employee `…0004` just before the insert; expect `DbUpdateException` with inner `PostgresException` `23503` / `fk_tasks_employees_assignee_id`, nothing persisted |

### BR → test mapping

| Rule | Domain (A2) | DB (B3) | Service (B3) |
|---|---|---|---|
| BR1 | `Create_ValidInput_StartsAsNewWithNullCompletedAt`, `ChangeStatus_TransitionTableRow_BehavesAsSpecified`, `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant` | `Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`, `Insert_NewWithCompletedAt_RejectedByBR1Check`, `Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check` | `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` |
| BR2 | `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`, `Create_DueAtEqualsPlannedStartAt_Succeeds`, `Create_PlannedStartAtOrDueAtMissing_Succeeds`, `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2` | `Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`, `Insert_DueAtEqualsPlannedStartAt_Accepted` | `CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` |
| BR3 | `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`, `Create_InactiveCreatorActiveAssignee_Succeeds` | `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger` | `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`, `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`, `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` |
| BR4 | `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 disallowed rows), `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt` | `Update_StatusOfCompletedTask_RejectedByBR4Trigger`, `Update_StatusOfCancelledTask_RejectedByBR4Trigger`, `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` | `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged` |
| BR5 | `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` | `Insert_AssigneeEqualsCreator_RejectedByBR5Check` | `CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` |

Failing-then-passing (FA/FB red evidence): disabling a domain guard turns its A2 row red; dropping a CHECK or the
trigger from a scratch copy of the migration turns its B3 rows red; removing the service's BR3 check or its
trigger-error translation turns the matching `TaskServiceTests` row red. BR1's domain guard is the `CompletedAt = …` assignment in
`ChangeStatus` (there is no throwing guard: the API cannot express a BR1 violation).

## 16. Verification

| Identifier | Verified where |
|---|---|
| Package versions and dependency ranges (EF 10.0.12, Relational 10.0.12, Design 10.0.12 `developmentDependency`, Npgsql EF 10.0.3 → Relational `[10.0.4, 11.0.0)`, Testcontainers.PostgreSql 4.15.0, xunit 2.9.3, runner 3.1.4, Test.Sdk 17.14.1) | `~/.nuget/packages/<id>/<version>/` folders and `*.nuspec` |
| `IEntityTypeConfiguration<T>.Configure`, `ModelBuilder.ApplyConfigurationsFromAssembly`, `EntityTypeBuilder<T>.HasKey/HasIndex/HasOne/HasData`, `PropertyBuilder<T>.HasMaxLength/IsRequired/ValueGeneratedNever/IsRowVersion/HasConversion<TProvider>()`, `IndexBuilder<T>.IsUnique`, `ReferenceNavigationBuilder<,>.WithMany`, `ReferenceCollectionBuilder<,>.HasForeignKey/OnDelete`, `DbSet<T>`, `DbContext.Set<T>()`, `SaveChangesAsync(CancellationToken)`, `EntityFrameworkQueryableExtensions.ToListAsync/SingleOrDefaultAsync/AsNoTracking`, `DbUpdateConcurrencyException`, `IDesignTimeDbContextFactory<T>.CreateDbContext(string[])` | `microsoft.entityframeworkcore/10.0.12/lib/net10.0/Microsoft.EntityFrameworkCore.xml` |
| `DeleteBehavior.Restrict` ("RESTRICT" FK) | `microsoft.entityframeworkcore.abstractions/10.0.12/.../Microsoft.EntityFrameworkCore.Abstractions.xml` |
| `ToTable<T>(string, Action<TableBuilder<T>>)`, `TableBuilder.HasCheckConstraint(string, string)` (inherited by `TableBuilder<T>`), `HasColumnName`, `HasColumnType`, `HasDatabaseName`, `KeyBuilder.HasName`, `HasConstraintName`, `MigrateAsync` (creates the DB), `ExecuteSqlRawAsync`, `SqlQueryRaw<T>`, `GetMigrations`, `GetAppliedMigrationsAsync`, `GetPendingMigrationsAsync` | `microsoft.entityframeworkcore.relational/10.0.12/.../Microsoft.EntityFrameworkCore.Relational.xml`; inheritance by reflection (pwsh on .NET 10.0.12) |
| `HasConversion<string>()` + `HasMaxLength(20)` gives a length-limited string column | https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions ("Column facets and mapping hints") |
| `[Obsolete]`: `EntityTypeBuilder.HasCheckConstraint` (all overloads), `IndexBuilder.HasName`; nothing else the spec names | reflection over EF 10.0.12 + Npgsql 10.0.3 assemblies |
| `UseNpgsql(string)`, xmin convention (`ProcessRowVersionProperty`: "uint, OnAddOrUpdate and configured as concurrency tokens … xmin") | `npgsql.entityframeworkcore.postgresql/10.0.3/.../Npgsql.EntityFrameworkCore.PostgreSQL.xml`; npgsql.org/efcore/modeling/concurrency.html; N0 probe migration |
| `PostgresException.SqlState/ConstraintName`, `PostgresErrorCodes` (has `CheckViolation`, `UniqueViolation`, `ForeignKeyViolation`), `NpgsqlConnectionStringBuilder.Database` | `npgsql/10.0.3/lib/net10.0/Npgsql.xml` + DLL metadata |
| Npgsql rejects non-zero offsets on `timestamptz` | Npgsql 10.0.3 DLL string; https://www.npgsql.org/doc/types/datetime.html |
| `PostgreSqlBuilder(string)` (parameterless ctor `[Obsolete]`), `PostgreSqlContainer.GetConnectionString()`, `IContainer.StartAsync(CancellationToken)` | `testcontainers.postgresql/4.15.0/.../Testcontainers.PostgreSql.xml`, `testcontainers/4.15.0/.../Testcontainers.xml`, reflection |
| `IAsyncLifetime`, `ICollectionFixture<T>`, `CollectionDefinitionAttribute(string)`, `CollectionAttribute(string)`, `TraitAttribute(string, string)`, `Assert.Throws<T>(Action)`, `Assert.ThrowsAsync<T>(Func<Task>)` | `xunit.*/2.9.3` XML docs |
| `Guid.CreateVersion7()`, `TimeProvider`, `TimeProvider.System`, `TimeProvider.GetUtcNow()` (virtual), `DateTimeOffset.ToUniversalTime()`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull`, `Enum.IsDefined<T>(T)`, `KeyNotFoundException(string)` | `Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0/System.Runtime.xml`, `System.Collections.xml`; `GetUtcNow` virtual by reflection |
| Analyzer severities (CA1510, CA2208, CA2201, CA1710, CA1711, CA1716, CA1720 = warning; CA1032, CA1515, CA2007, CA1062, CA1308 not enabled) | `sdk/10.0.401/.../analysislevel_10_recommended.globalconfig` |
| Every CHECK/FK/unique SQL text, SqlStates, constraint names, NULL handling, `ORDER BY due_at` NULLS LAST, `xmin` type `xid` | throwaway `postgres:17-alpine` (PostgreSQL 17.10) container |
| Trigger SQL (`CREATE FUNCTION … plpgsql`, `SELECT … FOR SHARE`, `RAISE … USING ERRCODE = 'check_violation', CONSTRAINT = …`, `BEFORE INSERT OR UPDATE OF status`), its SqlStates and constraint names, FK-not-masked behaviour, drop order | throwaway `postgres:17-alpine` container (orch, pre-implementation grill) |
| `MigrationBuilder.Sql(string, bool)`; `DbContextOptionsBuilder.UseAsyncSeeding` (considered for Q3, not used) | `microsoft.entityframeworkcore.relational/10.0.12` and `microsoft.entityframeworkcore/10.0.12` XML docs |
| Trigger cost: +21 µs/insert (lookup +11, `FOR SHARE` +10), +4 µs/status update, bulk +61% | throwaway container benchmark, 20,000 single-row statements and a 200,000-row insert (orch, grill) |
| `dotnet ef ... --connection`, `--no-connect`, `--startup-project` default | `dotnet ef ... --help`, dotnet-ef 10.0.12 |
| `ToTable(..., t => t.HasCheckConstraint(...))` compiles; `HasData` + `uint` row version (xmin excluded from `InsertData`); design-time factory on a class library | N0 probe in the orchestrator's scratchpad (`probe/src/Probe.Infra`) |
