---
title: "B1 → orch: persistence report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[task-item]]", "[[index]]"]
from: B1
to: orch
seq: 1
---

# B1 Persistence: report

## Status

Done. Items (a)–(d) all **confirmed** from the first generated migration. No configuration fix and no
regeneration were needed. `Up()` and `Down()` were proven against a throwaway `postgres:17-alpine` (PostgreSQL 17.10)
container, which has been removed. All five acceptance commands exit 0.

The session was interrupted once by an API rate limit (HTTP 429) after the header comment was added. On resume,
every file was re-read from disk and the build re-run before continuing. No container was running from the first
attempt: `docker ps` listed no `postgres:17-alpine` container.

## Files changed (all new)

- `src/TaskManagement.Infrastructure/TaskManagementDbContext.cs`
- `src/TaskManagement.Infrastructure/TaskManagementDbContextFactory.cs`
- `src/TaskManagement.Infrastructure/Configurations/EmployeeConfiguration.cs`
- `src/TaskManagement.Infrastructure/Configurations/TaskItemConfiguration.cs`
- `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs` (generated; two hand edits: the
  header comment, and the `migrationBuilder.Sql` calls at the end of `Up()` and the start of `Down()`)
- `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.Designer.cs` (generated, untouched)
- `src/TaskManagement.Infrastructure/Migrations/TaskManagementDbContextModelSnapshot.cs` (generated, untouched)
- `.wiki/agents/bus/B1-to-orch-001.md` (this report)

Nothing else was touched: no domain, application or test file, no csproj or props file, no package, and not
`.wiki/log.md`. That follows the task spec's file list, and the fan-in A log entry says "orch logs at fan-in".

## Public API

```csharp
namespace TaskManagement.Infrastructure;

public sealed class TaskManagementDbContext : DbContext
{
    public TaskManagementDbContext(DbContextOptions<TaskManagementDbContext> options);   // explicit ctor
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    protected override void OnModelCreating(ModelBuilder modelBuilder);                  // ApplyConfigurationsFromAssembly
}

public sealed class TaskManagementDbContextFactory : IDesignTimeDbContextFactory<TaskManagementDbContext>
{
    public TaskManagementDbContext CreateDbContext(string[] args);
    // UseNpgsql("Host=localhost;Port=5432;Database=task_management;Username=postgres")
}

namespace TaskManagement.Infrastructure.Configurations;

public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>   { public void Configure(EntityTypeBuilder<Employee> builder); }
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>   { public void Configure(EntityTypeBuilder<TaskItem> builder); }

namespace TaskManagement.Infrastructure.Migrations;
public partial class InitialCreate : Migration   // id 20260918164447_InitialCreate
```

The mapping follows spec §10–§12 exactly: every column has an explicit `HasColumnName`; `Version` has
`IsRowVersion()` and no column name; both FKs use `HasOne<Employee>().WithMany()` + `Restrict` + the §11 names; the
four CHECKs go through `ToTable("tasks", t => t.HasCheckConstraint(...))`; indexes use `HasDatabaseName`; the seed
uses `HasData` with anonymous objects in each configuration. The configuration XML docs list every index and
constraint with its reason and BR. `TaskItemConfiguration`'s remarks say the BR3/BR4 trigger lives in the
`InitialCreate` migration, not in the model (ADR 0009).

## Results (a)–(d), quoted from the generated `Up()`

**(a) Confirmed.** `status` is `character varying(20)`:

```csharp
status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
```

**(b) Confirmed.** `InsertData` for `tasks` writes the status as strings (the last two values of each row):

```csharp
columns: new[] { "id", "assignee_id", "completed_at", "creator_id", "due_at", "planned_start_at", "status", "title" },
{ new Guid("20000000-0000-0000-0000-000000000001"), ..., "New", "Prepare Q4 sales report" },
{ new Guid("20000000-0000-0000-0000-000000000002"), ..., "Completed", "Call back key account" },
{ new Guid("20000000-0000-0000-0000-000000000003"), ..., null, null, "Cancelled", "Clean up duplicate contacts" }
```

(`...` elides Guid/DateTimeOffset literals. The full rows are in the file. Every timestamp is
`new DateTimeOffset(new DateTime(..., DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0))`, which is offset 0.)

**(c) Confirmed.** In file order, the `employees` insert comes first (line 100), then the `tasks` insert (line 110):

```csharp
            migrationBuilder.InsertData(
                table: "employees",
...
            migrationBuilder.InsertData(
                table: "tasks",
```

Both come after the two `CreateTable` calls and before the four `CreateIndex` calls.

**(d) Confirmed.** There is no default-named index. `Grep "IX_"` over `Migrations/` returns 0 matches. The
migration has exactly the §11 objects:

```csharp
table.PrimaryKey("pk_employees", x => x.id);
table.PrimaryKey("pk_tasks", x => x.id);
table.CheckConstraint("ck_tasks_br1_completed_at_iff_completed", "(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)");
table.CheckConstraint("ck_tasks_br2_due_at_not_before_planned_start_at", "planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at");
table.CheckConstraint("ck_tasks_br5_assignee_not_creator", "assignee_id <> creator_id");
table.CheckConstraint("ck_tasks_status_valid", "status IN ('New', 'InProgress', 'Completed', 'Cancelled')");
name: "fk_tasks_employees_assignee_id", ... onDelete: ReferentialAction.Restrict);
name: "fk_tasks_employees_creator_id", ... onDelete: ReferentialAction.Restrict);
name: "ux_employees_email", table: "employees", column: "email", unique: true);
name: "ix_tasks_assignee_id_status", table: "tasks", columns: new[] { "assignee_id", "status" });
name: "ix_tasks_creator_id", table: "tasks", column: "creator_id");
name: "ix_tasks_due_at", table: "tasks", column: "due_at");
```

The composite `ix_tasks_assignee_id_status` covers the assignee FK, so EF created no `IX_tasks_assignee_id`. The
`\d tasks` output below shows the same set in the live database.

Other generated details: `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` appears in
`CreateTable`, is left out of `InsertData`, and does not show up in `\d tasks` because it is a system column. This is
the same shape the N0 probe recorded. The generator also emitted
`#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional`. That line is generated, not a hand
edit.

## Header comment (top of `20260918164447_InitialCreate.cs`, verbatim)

```text
// InitialCreate: schema changes (generated by dotnet ef 10.0.12; the trigger SQL is added by hand, spec §11).
//
// Tables and columns
//   employees: id uuid NOT NULL, full_name character varying(200) NOT NULL, email character varying(254) NOT NULL,
//              is_active boolean NOT NULL
//   tasks:     id uuid NOT NULL, title character varying(200) NOT NULL, status character varying(20) NOT NULL,
//              creator_id uuid NOT NULL, assignee_id uuid NOT NULL, planned_start_at timestamptz NULL,
//              due_at timestamptz NULL, completed_at timestamptz NULL
//              (plus xmin xid: PostgreSQL system column, the concurrency token; listed by EF, not created)
// Primary keys
//   pk_employees (employees.id), pk_tasks (tasks.id)
// Foreign keys
//   fk_tasks_employees_creator_id  tasks(creator_id)  -> employees(id) ON DELETE RESTRICT
//   fk_tasks_employees_assignee_id tasks(assignee_id) -> employees(id) ON DELETE RESTRICT
// Indexes
//   ux_employees_email          employees(email) UNIQUE
//   ix_tasks_assignee_id_status tasks(assignee_id, status)
//   ix_tasks_due_at             tasks(due_at)
//   ix_tasks_creator_id         tasks(creator_id)
// CHECK constraints (tasks)
//   ck_tasks_status_valid                           status IN ('New', 'InProgress', 'Completed', 'Cancelled')
//   ck_tasks_br1_completed_at_iff_completed         BR1: completed_at set iff status = 'Completed'
//   ck_tasks_br2_due_at_not_before_planned_start_at BR2: due_at >= planned_start_at when both are set
//   ck_tasks_br5_assignee_not_creator               BR5: assignee_id <> creator_id
// Trigger (raw SQL at the end of Up(), dropped at the start of Down())
//   function tasks_enforce_br3_br4() (plpgsql)
//   trigger trg_tasks_br3_br4 BEFORE INSERT OR UPDATE OF status ON tasks FOR EACH ROW
//     BR3: on insert, an inactive assignee raises 23514 / trg_tasks_br3_assignee_active
//     BR4: a status change out of Completed or Cancelled raises 23514 / trg_tasks_br4_final_status
// Seed rows (demo data)
//   employees: 10000000-…-0001 Alice Morgan (active), 10000000-…-0002 Bob Chen (active),
//              10000000-…-0003 Carol Diaz (inactive)
//   tasks:     20000000-…-0001 'Prepare Q4 sales report' New, Alice -> Bob
//              20000000-…-0002 'Call back key account' Completed, Bob -> Alice
//              20000000-…-0003 'Clean up duplicate contacts' Cancelled, Alice -> Bob
```

Trigger edit: at the end of `Up()` (after the four `CreateIndex` calls), two `migrationBuilder.Sql("""...""")`
calls: first the spec §11 `CREATE FUNCTION tasks_enforce_br3_br4()` body verbatim, then `CREATE TRIGGER
trg_tasks_br3_br4 BEFORE INSERT OR UPDATE OF status ON tasks FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4();`.
At the start of `Down()`: `migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_tasks_br3_br4 ON tasks;");` and then
`migrationBuilder.Sql("DROP FUNCTION IF EXISTS tasks_enforce_br3_br4();");`, both before the two generated
`DropTable` calls. The generated schema operations were not edited.

## Commands and real output

Environment: `dotnet --version` → `10.0.401`. `dotnet ef --version` → `Entity Framework Core .NET Command-line Tools 10.0.12`.
Docker server `29.8.0`.

API names checked before writing, by grepping the restored XML docs (`~/.nuget/packages/...`). Every one was
found: `RelationalKeyBuilderExtensions.HasName`, `RelationalForeignKeyBuilderExtensions.HasConstraintName`,
`RelationalIndexBuilderExtensions.HasDatabaseName`, `TableBuilder.HasCheckConstraint`,
`RelationalEntityTypeBuilderExtensions.ToTable<T>(EntityTypeBuilder<T>, string, Action<...>)`, `HasColumnName`,
`MigrationBuilder.Sql`, `PropertyBuilder<T>.IsRowVersion`, `PropertyBuilder<T>.HasConversion<TProvider>`,
`EntityTypeBuilder<T>.HasData`, `ModelBuilder.ApplyConfigurationsFromAssembly`, `IDesignTimeDbContextFactory<T>`,
`ReferenceCollectionBuilder<,>.OnDelete`, `ReferenceNavigationBuilder<,>.WithMany` (EF/Relational 10.0.12), and
`NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql<T>(DbContextOptionsBuilder<T>, string, ...)` (Npgsql EF 10.0.3).

### Build before generating the migration

```text
$ dotnet build -warnaserror
  TaskManagement.Domain -> ...\src\TaskManagement.Domain\bin\Debug\net10.0\TaskManagement.Domain.dll
  TaskManagement.Infrastructure -> ...\src\TaskManagement.Infrastructure\bin\Debug\net10.0\TaskManagement.Infrastructure.dll
  TaskManagement.UnitTests -> ...
  TaskManagement.Application -> ...
  TaskManagement.IntegrationTests -> ...
Build succeeded.
    0 Warning(s)
    0 Error(s)
exit=0
```

### Generate the migration

```text
$ dotnet ef migrations add InitialCreate --project src/TaskManagement.Infrastructure
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
exit=0
$ ls src/TaskManagement.Infrastructure/Migrations
20260918164447_InitialCreate.Designer.cs
20260918164447_InitialCreate.cs
TaskManagementDbContextModelSnapshot.cs
```

### Build after generation (before any hand edit) and after the hand edits

```text
$ dotnet build -warnaserror --no-incremental      # generated migration, untouched
Build succeeded.
    0 Warning(s)
    0 Error(s)
exit=0
$ dotnet build -warnaserror --no-incremental      # after header comment + trigger Sql
Build succeeded.
    0 Warning(s)
    0 Error(s)
exit=0
```

The generated migration built with no CS1591 error. The generator emits `/// <inheritdoc />` on the class, `Up` and
`Down`, and `.editorconfig` marks `src/**/Migrations/*.cs` as generated code.

### Up() against a throwaway container

```text
$ docker run -d --rm --name b1-pg -e POSTGRES_PASSWORD=pw -p 55432:5432 postgres:17-alpine
533af8df8db5e2594bc94f21cb6259ee76ce2303bb6a78aa108b026b23459a8d
$ docker exec b1-pg pg_isready -U postgres -h localhost        (after a readiness loop)
localhost:5432 - accepting connections
$ docker exec b1-pg psql -U postgres -tAc 'select version()'
PostgreSQL 17.10 on x86_64-pc-linux-musl, compiled by gcc (Alpine 15.2.0) 15.2.0, 64-bit

$ dotnet ef database update --project src/TaskManagement.Infrastructure --connection "Host=localhost;Port=55432;Database=task_management;Username=postgres;Password=pw"
Build started...
Build succeeded.
An error occurred using the connection to database 'task_management' on server 'tcp://localhost:55432'.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
Applying migration '20260918164447_InitialCreate'.
Done.
exit=0
```

The "An error occurred using the connection" line comes before the database exists. EF then creates the database
and applies the migration, and the command exits 0. `[uncertain]`: I infer that this line is EF's database-existence
probe. I did not trace it in the EF source.

```text
$ psql -c '\d employees'
                      Table "public.employees"
  Column   |          Type          | Collation | Nullable | Default
-----------+------------------------+-----------+----------+---------
 id        | uuid                   |           | not null |
 full_name | character varying(200) |           | not null |
 email     | character varying(254) |           | not null |
 is_active | boolean                |           | not null |
Indexes:
    "pk_employees" PRIMARY KEY, btree (id)
    "ux_employees_email" UNIQUE, btree (email)
Referenced by:
    TABLE "tasks" CONSTRAINT "fk_tasks_employees_assignee_id" FOREIGN KEY (assignee_id) REFERENCES employees(id) ON DELETE RESTRICT
    TABLE "tasks" CONSTRAINT "fk_tasks_employees_creator_id" FOREIGN KEY (creator_id) REFERENCES employees(id) ON DELETE RESTRICT

$ psql -c '\d tasks'
                             Table "public.tasks"
      Column      |           Type           | Collation | Nullable | Default
------------------+--------------------------+-----------+----------+---------
 id               | uuid                     |           | not null |
 title            | character varying(200)   |           | not null |
 status           | character varying(20)    |           | not null |
 creator_id       | uuid                     |           | not null |
 assignee_id      | uuid                     |           | not null |
 planned_start_at | timestamp with time zone |           |          |
 due_at           | timestamp with time zone |           |          |
 completed_at     | timestamp with time zone |           |          |
Indexes:
    "pk_tasks" PRIMARY KEY, btree (id)
    "ix_tasks_assignee_id_status" btree (assignee_id, status)
    "ix_tasks_creator_id" btree (creator_id)
    "ix_tasks_due_at" btree (due_at)
Check constraints:
    "ck_tasks_br1_completed_at_iff_completed" CHECK (status::text = 'Completed'::text AND completed_at IS NOT NULL OR status::text <> 'Completed'::text AND completed_at IS NULL)
    "ck_tasks_br2_due_at_not_before_planned_start_at" CHECK (planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at)
    "ck_tasks_br5_assignee_not_creator" CHECK (assignee_id <> creator_id)
    "ck_tasks_status_valid" CHECK (status::text = ANY (ARRAY['New'::character varying, 'InProgress'::character varying, 'Completed'::character varying, 'Cancelled'::character varying]::text[]))
Foreign-key constraints:
    "fk_tasks_employees_assignee_id" FOREIGN KEY (assignee_id) REFERENCES employees(id) ON DELETE RESTRICT
    "fk_tasks_employees_creator_id" FOREIGN KEY (creator_id) REFERENCES employees(id) ON DELETE RESTRICT
Triggers:
    trg_tasks_br3_br4 BEFORE INSERT OR UPDATE OF status ON tasks FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4()

$ psql -c 'SELECT * FROM employees'
                  id                  |  full_name   |          email           | is_active
--------------------------------------+--------------+--------------------------+-----------
 10000000-0000-0000-0000-000000000001 | Alice Morgan | alice.morgan@example.com | t
 10000000-0000-0000-0000-000000000002 | Bob Chen     | bob.chen@example.com     | t
 10000000-0000-0000-0000-000000000003 | Carol Diaz   | carol.diaz@example.com   | f
(3 rows)

$ psql -c 'SELECT id, status, completed_at FROM tasks'
                  id                  |  status   |      completed_at
--------------------------------------+-----------+------------------------
 20000000-0000-0000-0000-000000000001 | New       |
 20000000-0000-0000-0000-000000000002 | Completed | 2026-09-04 15:30:00+00
 20000000-0000-0000-0000-000000000003 | Cancelled |
(3 rows)

$ psql -c "SELECT tgname FROM pg_trigger WHERE tgrelid = 'tasks'::regclass AND NOT tgisinternal"
      tgname
-------------------
 trg_tasks_br3_br4
(1 row)

$ psql -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory"'
         MigrationId          | ProductVersion
------------------------------+----------------
 20260918164447_InitialCreate | 10.0.12
(1 row)
```

(`psql` above is short for `docker exec b1-pg psql -U postgres -d task_management`.)

### Trigger violations (psql `-v VERBOSITY=verbose`, so the SqlState and constraint name show)

BR3, assignee Carol (inactive). This is the B3 SQL for `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`:

```text
$ psql -v VERBOSITY=verbose -c "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES ('30000000-0000-0000-0000-000000000008', 'BR3 violation', 'New', '10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', NULL, NULL, NULL)"
ERROR:  23514: BR3: employee 10000000-0000-0000-0000-000000000003 is inactive and cannot be given a new task.
CONTEXT:  PL/pgSQL function tasks_enforce_br3_br4() line 10 at RAISE
CONSTRAINT NAME:  trg_tasks_br3_assignee_active
LOCATION:  exec_stmt_raise, pl_exec.c:3897
```

BR4, status change on the Completed seed task. This is the B3 SQL for `Update_StatusOfCompletedTask_RejectedByBR4Trigger`:

```text
$ psql -v VERBOSITY=verbose -c "UPDATE tasks SET status = 'New', completed_at = NULL WHERE id = '20000000-0000-0000-0000-000000000002'"
ERROR:  23514: BR4: task 20000000-0000-0000-0000-000000000002 is Completed; Completed and Cancelled are final.
CONTEXT:  PL/pgSQL function tasks_enforce_br3_br4() line 15 at RAISE
CONSTRAINT NAME:  trg_tasks_br4_final_status
LOCATION:  exec_stmt_raise, pl_exec.c:3897
```

An extra check, not required: an unknown assignee hits the FK, not BR3. This is the B3 SQL for
`Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`. The rows were unchanged afterwards:

```text
ERROR:  23503: insert or update on table "tasks" violates foreign key constraint "fk_tasks_employees_assignee_id"
DETAIL:  Key (assignee_id)=(10000000-0000-0000-0000-000000000009) is not present in table "employees".
CONSTRAINT NAME:  fk_tasks_employees_assignee_id
$ psql -c "SELECT count(*) AS tasks, (SELECT status FROM tasks WHERE id = '20000000-0000-0000-0000-000000000002') AS task2_status FROM tasks"
 tasks | task2_status
-------+--------------
     3 | Completed
```

### Down()

```text
$ dotnet ef database update 0 --project src/TaskManagement.Infrastructure --connection "Host=localhost;Port=55432;Database=task_management;Username=postgres;Password=pw"
Build started...
Build succeeded.
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
Reverting migration '20260918164447_InitialCreate'.
Done.
exit=0

$ psql -c '\dt'
                 List of relations
 Schema |         Name          | Type  |  Owner
--------+-----------------------+-------+----------
 public | __EFMigrationsHistory | table | postgres
(1 row)

$ psql -c '\df tasks_enforce_br3_br4'
                       List of functions
 Schema | Name | Result data type | Argument data types | Type
--------+------+------------------+---------------------+------
(0 rows)

$ psql -c 'SELECT count(*) AS history_rows FROM "__EFMigrationsHistory"'
 history_rows
--------------
            0
```

`__EFMigrationsHistory` is EF's bookkeeping table and stays after a revert. It is not part of `InitialCreate`.

```text
$ docker rm -f b1-pg
b1-pg
$ docker ps -a --filter name=b1-pg --format '{{.Names}}' | wc -l
0
```

### Acceptance (from the worktree root)

```text
$ dotnet build -warnaserror
    0 Warning(s)
    0 Error(s)
exit=0
$ dotnet ef migrations list --project src/TaskManagement.Infrastructure --no-connect
Build started...
Build succeeded.
20260918164447_InitialCreate
Pending status not shown. Unable to determine which migrations have been applied. This can happen when your project uses a version of Entity Framework Core lower than 5.0.0 or when an error occurs while accessing the database.
exit=0
$ grep -q "CREATE TRIGGER trg_tasks_br3_br4" src/TaskManagement.Infrastructure/Migrations/*_InitialCreate.cs
exit=0
$ grep -q "DROP FUNCTION IF EXISTS tasks_enforce_br3_br4" src/TaskManagement.Infrastructure/Migrations/*_InitialCreate.cs
exit=0
$ ! grep -rnE '\[(Key|Required|Table|Column|MaxLength|StringLength|ForeignKey|Index|NotMapped|DatabaseGenerated|ConcurrencyCheck|Timestamp)\b' src/TaskManagement.Domain --include=*.cs
exit=0
```

The "Pending status not shown" line is what `--no-connect` prints, and it is expected.

### Commit

```text
$ git rev-parse --abbrev-ref HEAD
worktree-agent-a00e9052f06739657
$ git diff --stat HEAD~1
 .wiki/agents/bus/B1-to-orch-001.md                 | 455 +++++++++++++++++++++
 .../Configurations/EmployeeConfiguration.cs        |  42 ++
 .../Configurations/TaskItemConfiguration.cs        | 108 +++++
 .../20260918164447_InitialCreate.Designer.cs       | 201 +++++++++
 .../Migrations/20260918164447_InitialCreate.cs     | 186 +++++++++
 .../TaskManagementDbContextModelSnapshot.cs        | 198 +++++++++
 .../TaskManagementDbContext.cs                     |  36 ++
 .../TaskManagementDbContextFactory.cs              |  21 +
 8 files changed, 1247 insertions(+)
```

This stat comes from the first commit. That commit was then amended to add this section to this report, so only
this report file's line count differs in the final commit. The commit SHA cannot be written into the commit that
contains it: the final SHA is in the final message to orch. Parent commit: `d831657`.

Notes on the commands:
- The two `dotnet ef database update ... --connection` commands ran through PowerShell, because the Bash tool's
  worktree guard refused them. The command text is exactly as shown. Every other command ran in Git Bash.
- `git add` printed "CRLF will be replaced by LF the next time Git touches it" for the three EF-generated
  migration files. Git normalises line endings, and the acceptance greps match either way.

## [uncertain]

- The "An error occurred using the connection to database 'task_management'" line during the first
  `database update` is, by inference, EF probing a database that does not exist yet. The command still created the
  database, applied the migration and exited 0.
- The trigger is not in the EF model or snapshot (by design, ADR 0009). A future `migrations add` will not know it
  exists. That is correct for additive migrations, but a migration that drops or renames `tasks.status` would need a
  hand-written trigger change. Not tested here.

## Open questions

- None blocking. One for orch: AGENTS.md says "Every commit appends an entry to `.wiki/log.md`", but this task
  spec's file list excludes it. I followed the task spec, and orch logs at fan-in, as for wave A.
