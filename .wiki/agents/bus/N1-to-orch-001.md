---
title: "N1 → orch: architecture spec report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[decisions/index]]", "[[orch-to-N1-001]]"]
from: N1
to: orch
seq: 1
---

# N1 report

**status: done** (spec written, nothing blocked; 3 open questions for the human gate).

## Files written

- `.wiki/domain/spec.md`: the full contract (§1–§16)
- `.wiki/domain/employee.md`, `.wiki/domain/task-item.md`
- `.wiki/domain/br1-completed-at.md`, `br2-due-not-before-start.md`, `br3-inactive-assignee.md`,
  `br4-final-statuses.md`, `br5-no-self-assignment.md`
- `docs/adr/0001-solution-layout.md` … `docs/adr/0008-business-rule-violation-exception.md` (8 ADRs)
- `.wiki/decisions/index.md`
- `.wiki/agents/bus/N1-to-orch-001.md` (this file)

No `.cs`/`.csproj`/solution files, no edits to orch-owned files, `.gitkeep`s untouched, no commit.
Scratch files (outside the repo): `<scratchpad>/n1/verify.sql`, `verify.out`.

## Decisions (≤ 15 lines)

1. Projects: Domain (no packages), Infrastructure (all EF/Npgsql), Application (`TaskService`, refs Infrastructure,
   no interface), **two** test projects: `TaskManagement.UnitTests` (Domain only) and `TaskManagement.IntegrationTests`.
2. Names: `TaskItem`, `TaskItemStatus { New, InProgress, Completed, Cancelled }`, `BusinessRuleViolationException(ruleId, message)` with `RuleId`.
3. Assignment: non-null `tasks.assignee_id`, set once at creation; no reassign operation. BR5 = row-local CHECK.
4. Keys: `Guid.CreateVersion7()` in the domain, `ValueGeneratedNever()`, `uuid`; fixed-Guid seeds, no sequence.
5. Status stored as text + `ck_tasks_status_valid`. Transition allowed iff current status not final; same-status on a
   non-final task is a no-op; 8 of 16 rows allowed.
6. Time: domain normalises every `DateTimeOffset` with `ToUniversalTime()`; `changedAt` is a parameter; service uses `TimeProvider`.
7. BR2: both dates nullable; checked only when both set; equal allowed.
8. BR3: "given a new task" = created with that assignee; checked in `TaskService.CreateTaskAsync` (DB-loaded row)
   and in `TaskItem.Create`. The creator's activity is not checked.
9. BR3/BR4: no DB constraint (ADR 0004). `TaskItem.Version` (`uint`, CLR property) ↔ `xmin` guards concurrent
   status changes (BR4); none on `Employee`.
10. E-mail: lowercase normalisation in the domain + plain unique index `ux_employees_email` (ADR 0005).
11. FKs: both `Restrict`. Every table, column, index and constraint name spelled out in spec §10–§11.
12. Not found: `KeyNotFoundException` for create/change; empty list for an unknown assignee.
13. Integration tests: one container per collection, a fresh migrated database per test.

## Verification: commands run and real output (trimmed)

| # | Command | Output |
|---|---|---|
| 1 | `ls ~/.nuget/packages/<id>` for the stack | EF 10.0.12, Relational 10.0.12, Design 10.0.12, Npgsql EF 10.0.3, Npgsql 10.0.3, Testcontainers.PostgreSql 4.15.0, xunit 2.9.3, runner 3.1.4, Test.Sdk 17.14.1 all present |
| 2 | `grep '<dependency' npgsql.entityframeworkcore.postgresql/10.0.3/*.nuspec` | `Microsoft.EntityFrameworkCore.Relational" version="[10.0.4, 11.0.0)"` |
| 3 | same on `microsoft.entityframeworkcore.design/10.0.12/*.nuspec` | `Microsoft.EntityFrameworkCore.Relational" version="10.0.12"`; `<developmentDependency>true` |
| 4 | `grep -oE 'name="M:…"'` over `Microsoft.EntityFrameworkCore.xml` 10.0.12 | found: `IDesignTimeDbContextFactory\`1.CreateDbContext(System.String[])`, `IEntityTypeConfiguration\`1.Configure`, `ApplyConfigurationsFromAssembly`, `HasData(System.Object[])`, `HasIndex`, `HasKey`, `HasOne\`\`1`, `IsUnique`, `HasConversion\`\`1`, `HasMaxLength`, `IsRequired`, `IsRowVersion`, `ValueGeneratedNever`, `HasForeignKey`, `OnDelete`, `WithMany`, `ToListAsync`, `SingleOrDefaultAsync`, `AsNoTracking`, `SaveChangesAsync(CancellationToken)`, `DbSet\`1.FindAsync`, `DbContext.Set\`\`1`, `T:…DbUpdateConcurrencyException` |
| 5 | same over `Microsoft.EntityFrameworkCore.Abstractions.xml` | `F:Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict`: "will configure the foreign key constraint as "RESTRICT"" |
| 6 | same over `Microsoft.EntityFrameworkCore.Relational.xml` | `ToTable\`\`1(…,System.String,System.Action{…TableBuilder{\`\`0}})`, `TableBuilder.HasCheckConstraint(System.String,System.String)`, `HasColumnName`, `HasColumnType`, `UseCollation`, `HasDatabaseName`, `KeyBuilder…HasName`, `HasConstraintName\`\`2(ReferenceCollectionBuilder…)`, `MigrateAsync` ("Will create the database if it does not already exist"), `ExecuteSqlRawAsync`, `SqlQueryRaw\`\`1`, `GetMigrations`, `GetAppliedMigrationsAsync`, `GetPendingMigrationsAsync` |
| 7 | `grep -a 'ToTable(t => t.HasCheckConstraint' Microsoft.EntityFrameworkCore.Relational.dll` | `Configure this using ToTable(t => t.HasCheckConstraint()) instead.` |
| 8 | pwsh reflection over EF 10.0.12 + Npgsql 10.0.3 DLLs listing `[Obsolete]` among the named methods | only `HasCheckConstraint(EntityTypeBuilder…/OwnedNavigationBuilder…)` (8 overloads) and `RelationalIndexBuilderExtensions.HasName(IndexBuilder,String): Use HasDatabaseName() instead.` |
| 9 | pwsh reflection: `TableBuilder\`1` base type; `TimeProvider.GetUtcNow` | `TableBuilder1 base: …TableBuilder`, `TableBuilder.HasCheckConstraint(String name, String sql) -> CheckConstraintBuilder`; `GetUtcNow IsVirtual=True` (runtime .NET 10.0.12) |
| 10 | `grep` over `Npgsql.EntityFrameworkCore.PostgreSQL.xml` 10.0.3 | `UseNpgsql(…String…)`, `HasCollation(ModelBuilder,String,String,String,Nullable{Boolean})`, `HasPostgresExtension`; `ProcessRowVersionProperty`: "Detects properties which are uint, OnAddOrUpdate and configured as concurrency tokens, and maps these to the PostgreSQL internal "xmin" column" |
| 11 | `grep` over `Npgsql.xml` 10.0.3 + python scan of `Npgsql.dll` | `P:Npgsql.PostgresException.SqlState`, `.ConstraintName`, `T:Npgsql.PostgresErrorCodes`, `T:Npgsql.NpgsqlConnectionStringBuilder`, `.Database`; DLL has `CheckViolation`, `UniqueViolation`, `ForeignKeyViolation`; string `'Cannot write DateTimeOffset with Offset='` + `" to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported. "` |
| 12 | `Select-Xml` on `Testcontainers.PostgreSql.xml` 4.15.0 + pwsh reflection | ctors `()`, `(System.String)`, `(IImage)`; `ctor() obsolete=True This parameterless constructor is obsolete and will be removed. Use the constructor with the image parameter instead…`; `ctor(String) obsolete=False`; `GetConnectionString`; `IContainer.StartAsync(CancellationToken)` |
| 13 | `grep` over `xunit.*/2.9.3` XML | `IAsyncLifetime.InitializeAsync/DisposeAsync`, `IClassFixture\`1`, `ICollectionFixture\`1`, `CollectionDefinitionAttribute.#ctor(System.String)`, `CollectionAttribute.#ctor(System.String)`, `TraitAttribute.#ctor(System.String,System.String)`, `Assert.Throws\`\`1(System.Action)`, `Assert.ThrowsAsync\`\`1(System.Func{Task})` |
| 14 | `grep` over `Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0/System.Runtime.xml`, `System.Collections.xml` | `Guid.CreateVersion7`, `Guid.CreateVersion7(System.DateTimeOffset)`, `T:System.TimeProvider`, `P:System.TimeProvider.System`, `M:System.TimeProvider.GetUtcNow`, `ArgumentException.ThrowIfNullOrWhiteSpace(String,String)`, `ArgumentNullException.ThrowIfNull(Object,String)`, `Enum.IsDefined\`\`1`, `DateTimeOffset.ToUniversalTime`, `KeyNotFoundException.#ctor(System.String)` |
| 15 | `grep` over `sdk/10.0.401/…/analysislevel_10_recommended.globalconfig` | warning: CA1036 CA1051 CA1068 CA1304 CA1305 CA1309 CA1310 CA1311 CA1510–CA1513 CA1707 CA1708 CA1710 CA1711 CA1716 CA1720 CA1725 CA1805 CA1822 CA1848 CA1852 CA1860 CA1862 CA1865 CA1866 CA1869 CA2016 CA2201 CA2208 CA2263 CA1000 CA1001; absent: CA1032, CA1515, CA2007, CA1062, CA1308, CA1002 (present as warning only in `_all`) |
| 16 | `docker run postgres:17-alpine` + `psql < verify.sql` (all DDL, seed and violations of spec §10–§12, §15) | `PostgreSQL 17.10 … musl`; seed `INSERT 0 3` ×2; Bob's list `…0001 (2026-10-10) , …0003 (null)`; `xmin_type = xid`; `23514 … "ck_tasks_br1_completed_at_iff_completed"` ×3 (insert Completed w/o date, insert New with date, update clearing date); `23514 … "ck_tasks_br2_due_at_not_before_planned_start_at"`; `23514 … "ck_tasks_br5_assignee_not_creator"`; `23514 … "ck_tasks_status_valid"`; `23505 … "ux_employees_email"`; delete Bob → `23503 … "fk_tasks_employees_creator_id"`; unknown assignee → `23503 … "fk_tasks_employees_assignee_id"`; due = planned → `INSERT 0 1`; due only → `INSERT 0 1` |
| 17 | `dotnet ef database update --help`, `dotnet ef migrations list --help`, `dotnet ef --version` | `--connection <CONNECTION> The connection string to the database.`; `-s\|--startup-project … Defaults to the current working directory.`; `--no-connect Don't connect to the database.`; `10.0.12` |
| 18 | read the N0 probe (`<orch scratchpad>/probe/src/Probe.Infra/*`, `tests/Probe.Tests/*`) | `ToTable("employees", t => t.HasCheckConstraint(...))` compiled; migration has `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` and `InsertData` columns `Id, CreatedAt, full_name` (no xmin); unconfigured names stay `Id`, `PK_employees`; design-time factory `UseNpgsql("Host=localhost;Database=probe")`; `new PostgreSqlBuilder("postgres:17-alpine")` |
| 19 | WebFetch npgsql.org/doc/types/datetime.html, /efcore/modeling/concurrency.html, /efcore/misc/collations-and-case-sensitivity.html | "supports reading and writing DateTimeOffset to `timestamp with time zone`, but only with Offset=0"; `.Property(b => b.Version).IsRowVersion()` maps `uint` to `xmin`; collations "the recommended way" for case-insensitivity, LIKE limitations on older PostgreSQL |
| 19b | WebFetch learn.microsoft.com/en-us/ef/core/modeling/value-conversions | "These facets can be configured in the normal way for a property that uses a value converter, and will apply to the converted database type"; example `.HasConversion<string>().HasMaxLength(20)` → `varchar(20)` (SQL Server) |
| 20 | N1 acceptance from graph.yaml + wikilink/relative-link resolver | `ACCEPTANCE EXIT=0`; all 10 wikilink targets and 8 ADR links resolve |

Identifier → source is tabulated in spec §16.

## [uncertain]

1. Solution-level `dotnet test --filter Category=Unit` exits 0 when `TaskManagement.IntegrationTests` matches zero
   tests (S0 should run it on the scaffold).
2. dotnet-ef falls back to `--project` as the startup project when the working directory has no project; help says
   the default is the working directory. The spec's commands pass `--startup-project` explicitly; the AGENTS.md
   commands (orch-owned) do not.
3. EF adds no extra `IX_tasks_assignee_id` FK index when `ix_tasks_assignee_id_status` covers it (B1 checks `Up()`).
4. EF/Npgsql emits a plain `ORDER BY due_at, id` for `OrderBy(t => t.DueAt).ThenBy(t => t.Id)` (so nulls sort last).
5. `ExecuteSqlRawAsync` surfaces `PostgresException` unwrapped.
6. CA1001 on the B3 fixture that holds an `IAsyncDisposable` container and implements only `IAsyncLifetime`.
7. `SqlQueryRaw<string>` needs the column aliased `"Value"` when not composed (the spec aliases it anyway).
8. .NET `ToLowerInvariant()` vs PostgreSQL `lower()` on non-ASCII (only matters for the rejected CHECK in ADR 0005).
9. `HasData` with the `HasConversion<string>()` status emits the strings `"New"`/`"Completed"`/`"Cancelled"` in
   `InsertData` (not integers), and `employees` inserts precede `tasks` inserts. The N0 probe seeded one table with no
   converter. Not probed here because N1 may not write `.cs`/`.csproj` files; B1 confirms from the generated `Up()`,
   and B3's `Seed_Tasks_MatchSpec` catches a mismatch.
10. Npgsql's exact store type for the converted enum is `character varying(20)`: Learn says facets apply to the
    converted type (SQL Server example gives `varchar(20)`); B1 confirms from `Up()`.

## Open questions for the human (max 3)

1. **Approve `Microsoft.EntityFrameworkCore.Relational` 10.0.12 as an explicit reference in Infrastructure?**
   Recommended default: **yes**. Without it every project referencing Infrastructure fails with MSB3277 (N0 probe),
   and the spec has no approved alternative.
2. **E-mail case-insensitivity: lowercase in the domain + plain unique index (DB guarantees exact-match only), or an
   ICU nondeterministic collation so the DB itself enforces case-insensitive uniqueness?** Recommended default:
   **domain lowercase** (ADR 0005); switching later is one migration with the same column and index names.
3. **Is "assigned once at creation, no reassignment, no unassigned tasks" acceptable?** Recommended default:
   **yes** (ADR 0002): the brief's three use cases never reassign, and the column model keeps BR5 a DB CHECK if
   reassignment is added later.

## Notes for orch

- Transition rule choices the human may want to see at the gate (not asked as questions): `New → Completed` and
  `InProgress → New` are allowed; same-status on a non-final task is a no-op; the creator may be inactive.
- `graph.yaml` provisional paths: the test projects are `tests/TaskManagement.UnitTests` and
  `tests/TaskManagement.IntegrationTests`; A2/B3 `outputs` can be filled from spec §1 "Files per node".
