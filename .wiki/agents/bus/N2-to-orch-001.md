---
title: "N2 → orch: plan review report"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[orch-to-N2-001]]", "[[N1-to-orch-001]]", "[[decisions/index]]", "[[index]]"]
from: N2
to: orch
seq: 1
---

# N2 plan review

## Verdict

**approve with fixes**: 0 blocker, 1 major, 7 minor.

The spec pins every name that A1/A2 and B1/B2/B3 share, and covers every item in THE TASK, LOCKED DEFAULTS and the
N1/B1/B2/B3 graph items. Every API named in orch's minimum sample exists in the restored 10.0.12 / 10.0.3 / 4.15.0
packages or the 10.0.12 ref pack. I found nothing invented and nothing out of scope. The one major finding is that
the brief's DOCUMENTATION STANDARD never reaches B1, B2 or B3: it is not in the spec, and `AGENTS.md` (where it
lives) is not in those nodes' graph inputs.

Pre-existing changes, not mine: ` M .wiki/index.md` and ` M .wiki/plan/graph.yaml` were already in the first
`git status --short` I ran, before I wrote anything (orch's post-N1 graph and index updates). My only write is this file.

## Findings

| id | severity | category | location | what is wrong | evidence |
|---|---|---|---|---|---|
| F1 | major | missing / hazard | `.wiki/domain/spec.md` (whole; §2 lists analyzers only); `.wiki/plan/graph.yaml` B1 (line 165), B2 (185), B3 (197) `inputs` | The brief's DOCUMENTATION STANDARD is missing from the spec. The spec does not require XML docs (`<summary>`, `<param>`, `<returns>`, `<exception>`, plus `<remarks>` naming BR1–BR5) on every type and member. It also does not require each Fluent config class to document which index or constraint it creates and why. It mentions only `<exception>` for the service (§14) and the migration header comment (§1, B1 row). The literal shapes in §3.3 (enum) and §3.4 (exception class) have no doc comments, so a worker who copies them as written hits CS1591, which is a build error here. The standard lives in `AGENTS.md` (Conventions), which A1 and A2 get as input but **B1, B2 and B3 do not**. B2 and B3 cannot compile inside their worktrees (graph acceptance defers compile to FB), so missing docs on B2's service and on B3's public test classes and methods would first fail at FB. | `grep -niE 'doc(s\|ument)\|CS1591\|summary' .wiki/domain/spec.md` returned only ADR links, line 32 ("analyzers and docs come from `Directory.Build.props`"), line 441 and 450 (`<exception>`), and 512/589 (verification rows). No XML-doc or `<remarks>` requirement. `grep -nE '^    inputs:' .wiki/plan/graph.yaml` → `116: [spec.md, .wiki/domain/*.md, AGENTS.md]` (A1), `134: [..., AGENTS.md]` (A2), `165: [.wiki/domain/spec.md, docs/adr/**, "src/TaskManagement.Domain/**"]` (B1), `185: [.wiki/domain/spec.md, "src/TaskManagement.Domain/**"]` (B2), `197: [.wiki/domain/spec.md]` (B3). `Directory.Build.props` line 13: `<WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors>`. Fix options for orch: add a "Documentation" subsection to spec §2, or add `AGENTS.md` to the B1/B2/B3 inputs, or both. |
| F2 | minor | hazard | spec §2 "Analyzer rules that are errors here" | The list is incomplete for test and fixture code. `xunit` 2.9.3 pulls in `xunit.analyzers` 1.18.0, whose warnings become errors under `TreatWarningsAsErrors`. The spec does not mention them. CA1861 (constant arrays as arguments), CA1859, CA1816, CA1051 and CA1001 are also warnings (so errors) and are plausible in B3's fixture and assertions, but §2 does not list them. | `grep -oE '<dependency ...' xunit/2.9.3/*.nuspec` → `xunit.analyzers" version="1.18.0"`. `analysislevel_10_recommended.globalconfig`: `CA1861.severity = warning`, `CA1859 = warning`, `CA1816 = warning`, `CA1051 = warning`, `CA1001 = warning`. `[uncertain]`: I did not check individual xUnit rule ids or their severities. |
| F3 | minor | inconsistent | spec §1 lines 36–37; spec §13 lines 409–411; ADR 0001 lines 35–36; `.wiki/log.md` | Two `[uncertain]` markers that orch says it resolved (solution-level `--filter Category=Unit` with a zero-match project; dotnet-ef falling back to `--project`) are still in the spec and in ADR 0001. Orch's evidence for resolving them is not in the repo: the last `log.md` entry is the N0 CodeGraph correction, and there is no N1 entry. The `dotnet ef` commands are also written two ways: with `--startup-project` in spec §13, and without it in `AGENTS.md` lines 68–70 and graph.yaml B1/FB acceptance. Per orch's resolution both forms work, but they differ. | `.wiki/log.md` read in full (2 entries, both N0). Spec line 36: "`[uncertain]` whether `dotnet test --filter Category=Unit` at the solution level exits 0 …". Spec line 410: "`[uncertain]` whether dotnet-ef falls back to `--project` …". `dotnet ef migrations list --help` (10.0.12): "`-s\|--startup-project <PROJECT>` The startup project to use. Defaults to the current working directory." I did not re-run orch's two resolutions (not reproduced). |
| F4 | minor | inconsistent | `.wiki/plan/graph.yaml` line 171 (B1 `outputs`) | The entry `src/TaskManagement.Infrastructure/Migrations/<timestamp>_InitialCreate.cs (+ .Designer.cs)` is neither an exact path nor a glob. The brief asks for "outputs (exact paths)", and the FB gate ("`git diff --stat` touches only the node's declared outputs") cannot match this entry mechanically. | Line 171 as quoted. Brief, EXECUTION GRAPH: "outputs (exact paths)"; VERIFICATION GATES: "`git diff --stat` touches only the node's declared outputs". |
| F5 | minor | hazard (test gap) | spec §14 `CreateTaskAsync` steps 3–4; §3.2 `Create` step 6; §15 `TaskServiceTests` | The service-level BR3 check (§14 step 3) has no test that fails when only that check is removed. `TaskItem.Create` then re-checks BR3 on the same loaded `Employee` and throws the same type with the same `RuleId "BR3"`, so `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing` stays green. The brief's "checked here and in the domain" is met in code, but no failing-then-passing test proves the service half. | Spec §14 step 3 and step 4 ("domain re-checks BR5, BR3, BR2"); §3.2 step 6 (same exception, same `RuleId`); §15 says tests assert `RuleId`, never message text. Follows from the spec text; I executed nothing. |
| F6 | minor | inconsistent | `.wiki/domain/br3-inactive-assignee.md` line 24 vs spec §15 "BR → test mapping" BR3 row | The BR3 page lists `Deactivate_ActiveEmployee_SetsIsActiveFalse` as a BR3 test. The spec's mapping row does not (§15 `EmployeeTests` calls it a "BR3 precondition"). Every other test name on every BR page matches the spec's mapping and test tables. | Cross-check script: extracted every `Method_Scenario_Expected` name from `br*.md` and the §15 mapping, then compared them against the §15 test tables (60 names defined, 0 undefined). Only output: `BR3 page lists Deactivate_ActiveEmployee_SetsIsActiveFalse; spec mapping row does not`. |
| F7 | minor | inconsistent | `.wiki/index.md` lines 25–28 | The index still says "Phase: N0 bootstrap. Next: N1 …" and "Solution layout: not decided yet (N1)". It does not link `[[spec]]`, the entity pages, the BR pages or `decisions/index`. The brief calls the index a "map of every page". The index is orch-owned, and N1 was not allowed to edit it. | `.wiki/index.md` read in full. |
| F8 | minor | missing | `.wiki/plan/graph.yaml` D1 `acceptance` (line 229) | D1's acceptance ("every `[[wikilink]]` resolves … and every code path cited in .wiki/ and README.md exists") would pass on a README that has only a title. It does not check the four contents the brief requires: prerequisites, how to run migrations, how to run tests, and where each BR is enforced. It also does not check the BR1–BR5 → test mapping that VERIFICATION GATES says must be "listed in the README". | graph.yaml line 229 as quoted. Brief, DOCUMENTATION STANDARD: "`README.md`: prerequisites, how to run migrations, how to run tests, and where each BR is enforced". VERIFICATION GATES: "BR1–BR5 each map to at least one failing-then-passing test, listed in the README." |

Categories with no findings:
- **invented**: none. Every identifier I sampled exists (table below).
- **out-of-scope**: none. There is no web API, UI, host, repository, interface, DI container or extra feature. The
  single custom exception (ADR 0008), the design-time factory (needed because `dotnet ef` runs on a class library with
  no host) and `Employee.Create`/`Deactivate` (needed to express BR3 and seed/test employees) all trace back to the
  brief. The extra CHECK and index are listed under Decisions.

## Coverage matrix

| Brief requirement | Spec location | Status |
|---|---|---|
| Employees | §3.1, §10 `employees`, [[employee]] | covered |
| Tasks | §3.2, §10 `tasks`, [[task-item]] | covered |
| Task assignment | §4, ADR 0002 (column, set once) | covered |
| Deadlines | §3.2 `DueAt`/`PlannedStartAt`, §7 | covered (both nullable; see Decisions) |
| Statuses | §3.3, §5, ADR 0007 | covered |
| BR1 domain / DB / test | §3.2 `Create` step 8 + `ChangeStatus` step 4 (structural) / `ck_tasks_br1_completed_at_iff_completed` §11 / §15 mapping | covered |
| BR2 domain / DB / test | §3.2 step 7 / `ck_tasks_br2_due_at_not_before_planned_start_at` / §15 | covered |
| BR3 domain / DB / test | §3.2 step 6 / none, not expressible as a CHECK (ADR 0004) / §15 | covered (F5: service half not provable by test) |
| BR4 domain / DB / test | §3.2 `ChangeStatus` step 2 / none, not expressible as a CHECK (ADR 0004) + `xmin` token / §15 | covered |
| BR5 domain / DB / test | §3.2 step 5 / `ck_tasks_br5_assignee_not_creator` / §15 | covered |
| BR3 in application and domain | §8, §14 step 3, §3.2 step 6 | covered |
| Use case: create a task | §14 `CreateTaskAsync` | covered |
| Use case: change a task's status | §14 `ChangeTaskStatusAsync` | covered |
| Use case: list tasks by assignee | §14 `ListTasksByAssigneeAsync` | covered |
| Index assignee + status | §11 `ix_tasks_assignee_id_status` | covered |
| Index due date | §11 `ix_tasks_due_at` | covered |
| Unique e-mail | §11 `ux_employees_email`, ADR 0005 | covered |
| Check constraints BR1, BR2, BR5 | §11 (plus `ck_tasks_status_valid`) | covered |
| FK delete behaviours | §10 (both `Restrict`, named) | covered |
| Concurrency token | §10 `TaskItem.Version` ↔ `xmin`, ADR 0004 | covered |
| Seed 2–3 rows per table | §12 (3 employees, 3 tasks) | covered; checked against every CHECK/FK/unique rule (see below) |
| Assignment ADR | ADR 0002 | covered |
| Enum + transition table | §3.3, §5 (16 rows) | covered |
| `DateTimeOffset` / `timestamptz` / UTC | §6, ADR 0006, §10 store types | covered |
| async + `CancellationToken` | §14 signatures | covered |
| Solution layout, each project justified | §1, ADR 0001 | covered |
| Package versions from the approved stack | §1 table | covered (Relational pin pending, see Decisions) |
| xUnit + Testcontainers, no in-memory for constraints | §1, §15 | covered |
| Nullable / TWAE / latest-recommended / CS1591 | `Directory.Build.props`; spec §2 (analyzers) | analyzers covered; **XML-doc requirement MISSING from spec** (F1) |
| XML docs + `<remarks>` naming BR on every type/member | none in spec; only in `AGENTS.md` | **MISSING** (F1) |
| Fluent config classes document their index/constraint and why | none in spec; only in `AGENTS.md` | **MISSING** (F1) |
| Migration header comment | §1 "Files per node", B1 row | covered |
| One ADR per non-trivial decision | `docs/adr/0001`–`0008`, `.wiki/decisions/index.md` | covered |
| README (prereqs, migrations, tests, where each BR is enforced) | graph.yaml D1 (not N1's scope); BR pages hold the "where enforced" data | assigned to D1, but D1's acceptance does not check the contents (F8) |
| Graph node outputs match spec §1 | S0/A1/A2/B1/B2/B3 compared file by file | match (F4: B1 migration entry not exact) |
| B3: DB rejects BR1/BR2/BR5; migration on empty DB; seed loads | §15 `DatabaseConstraintTests`, `MigrationAndSeedTests` | covered |
| FB: `ef migrations list`, apply to a container, full suite | graph.yaml FB | covered |

**Internal consistency (check 4).** The following cross-checks came back clean:
- All 12 DB object names are spelled identically in the spec, the entity pages, the BR pages, the ADRs and graph.yaml.
  `grep -ohE '\b(ck|ix|ux|fk|pk)_[a-z0-9_]+'` found exactly the 12 names of §11 and nothing else.
- `Cancelled` has no single-L spelling anywhere.
- No type is named `Task` or `TaskStatus`.
- The namespaces are consistent (`TaskManagement.Domain`, `.Infrastructure`, `.Infrastructure.Configurations`,
  `.Infrastructure.Migrations`, `.Application`, `.UnitTests`, `.IntegrationTests`).
- The transition table, `ChangeStatus` steps 1–4 and the A2 theory description agree. BR4 (step 2) runs before the
  same-status no-op (step 3), so `Completed → Completed` is rejected in all three. There are 8 allowed and 8 rejected
  rows.
- The seed data satisfies every rule:
  - Only row …0002 is `Completed`, and it is the only row with a non-null `CompletedAt` (BR1).
  - `DueAt` ≥ `PlannedStartAt` or null on every row (BR2).
  - Creator ≠ assignee on all 3 rows (BR5).
  - Carol (inactive) is assigned no task (BR3).
  - Every FK target exists, the statuses are valid, and the e-mails are lowercase and distinct.
- The B3 expectations match the seed: Bob's list is `[…0001, …0003]` with the null `DueAt` last, and Carol has no tasks.
- The e-mail boundary tests are correct: `"@example.com"` is 12 characters, so 242 + 12 = 254 and 243 + 12 = 255.

**Parallel-work hazards (check 5).**
- **A1 ∥ A2:** every shared name is pinned: types, the namespace, factory signatures, guard order, exception types and
  `RuleId` values, constants, enum members. A2 has to guess nothing.
- **B1 ∥ B2 ∥ B3:** the DbContext type, constructor and `DbSet` names (§13), the `TaskService` constructor and the
  three signatures (§14), and all SQL names (§10–§11) are pinned.
- The only names B3 invents live in B3's own files: the fixture API and a `TimeProvider` subclass.
- The residual risk is structural, not a naming gap: B2 and B3 compile for the first time at FB (graph acceptance),
  which is why F1 matters.

## Verified identifiers

`P` = `~/.nuget/packages`; ref pack = `C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.12/ref/net10.0`.
Reflection ran in `pwsh` on `.NET 10.0.12` (scripts in my scratchpad, outside the repo).

| Identifier | Where checked | Result (trimmed) |
|---|---|---|
| Package folders EF/Relational/Design/Abstractions 10.0.12, Npgsql + Npgsql EF 10.0.3, Testcontainers(+PostgreSql) 4.15.0, xunit 2.9.3, runner 3.1.4, Test.Sdk 17.14.1 | `ls -d $P/<id>/<ver>` | all `present` |
| Relational pin rationale | `grep '<dependency' $P/npgsql.entityframeworkcore.postgresql/10.0.3/*.nuspec`; same on `microsoft.entityframeworkcore.design/10.0.12` | `Microsoft.EntityFrameworkCore.Relational" version="[10.0.4, 11.0.0)"`, `Npgsql" version="10.0.3"`; Design: `Relational" version="10.0.12"`, `<developmentDependency>true` |
| Testcontainers.PostgreSql deps | nuspec | only `Testcontainers 4.15.0` (no Npgsql, so no version clash) |
| `TableBuilder.HasCheckConstraint(string, string)` | Relational XML; reflection | `M:…Builders.TableBuilder.HasCheckConstraint(System.String,System.String)`; `TableBuilder\`1 base: …TableBuilder`; `obsolete=False` |
| `EntityTypeBuilder.HasCheckConstraint` (old form) | reflection on `RelationalEntityTypeBuilderExtensions` | `overloads=9 obsolete=8` (so the spec's `ToTable(..., t => t.HasCheckConstraint(...))` form is required) |
| `ToTable<T>(string, Action<TableBuilder<T>>)` | Relational XML | `M:…RelationalEntityTypeBuilderExtensions.ToTable\`\`1(…EntityTypeBuilder{\`\`0},System.String,System.Action{…TableBuilder{\`\`0}})`; reflection `ToTable overloads=22 obsolete=0` |
| `IsRowVersion` → `xmin` | EF XML `M:…PropertyBuilder\`1.IsRowVersion`; Npgsql EF XML `ProcessRowVersionProperty`; N0 probe migration | "Detects properties which are uint, OnAddOrUpdate and configured as concurrency tokens, and maps these to the PostgreSQL internal "xmin" column"; probe line 22: `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)` |
| `HasConversion<string>()` | EF XML | `M:…PropertyBuilder\`1.HasConversion\`\`1` (parameterless generic); reflection `overloads=32 obsolete=0` |
| `HasConstraintName` on `ReferenceCollectionBuilder<,>` | Relational XML; reflection | `M:…RelationalForeignKeyBuilderExtensions.HasConstraintName\`\`2(…ReferenceCollectionBuilder{\`\`0,\`\`1},System.String)`; `overloads=7 obsolete=0` |
| `HasDatabaseName` on `IndexBuilder<T>` | Relational XML; reflection | `M:…RelationalIndexBuilderExtensions.HasDatabaseName\`\`1(…IndexBuilder{\`\`0},System.String)`; `HasDatabaseName obsolete=0`; the sibling `RelationalIndexBuilderExtensions.HasName: overloads=1 obsolete=1` |
| `HasKey(...).HasName("pk_…")` | reflection; Relational XML | `HasKey(Expression\`1 keyExpression) -> KeyBuilder`; `RelationalKeyBuilderExtensions.HasName: overloads=3 obsolete=0`; `M:…RelationalKeyBuilderExtensions.HasName(…KeyBuilder,System.String)` |
| `HasOne<Employee>().WithMany().HasForeignKey(...).OnDelete(...)` binds | reflection on `EntityTypeBuilder\`1` / `ReferenceNavigationBuilder\`2`; EF XML | `HasOne<T>(String navigationName)` (required) and `HasOne<T>(Expression\`1 navigationExpression =opt)`, so `HasOne<Employee>()` binds to the expression overload; `WithMany(String navigationName =opt) -> ReferenceCollectionBuilder\`2`; `M:…ReferenceCollectionBuilder\`2.HasForeignKey(System.Linq.Expressions.Expression{System.Func{\`1,System.Object}})`; `…ReferenceCollectionBuilder\`2.OnDelete(…DeleteBehavior)` |
| `DeleteBehavior.Restrict` | Abstractions XML | "… will configure the foreign key constraint as "RESTRICT" or "NO ACTION"" |
| `HasIndex`, `IsUnique`, `HasData(object[])`, `ValueGeneratedNever`, `HasMaxLength`, `IsRequired`, `ApplyConfigurationsFromAssembly`, `SaveChangesAsync(CancellationToken)`, `SingleOrDefaultAsync`, `ToListAsync`, `AsNoTracking`, `DbUpdateConcurrencyException`, `IDesignTimeDbContextFactory\`1.CreateDbContext(string[])` | EF XML grep | all present (e.g. `M:…EntityTypeBuilder\`1.HasData(System.Object[])`, `M:…EntityFrameworkQueryableExtensions.SingleOrDefaultAsync\`\`1(…,Expression{Func{\`\`0,Boolean}},CancellationToken)`, `T:Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException`) |
| `Database.SqlQueryRaw<T>` | Relational XML; reflection | `M:…RelationalDatabaseFacadeExtensions.SqlQueryRaw\`\`1(…DatabaseFacade,System.String,System.Object[])`; `obsolete=0` |
| `ExecuteSqlRawAsync`, `MigrateAsync`, `GetMigrations`, `GetAppliedMigrationsAsync`, `GetPendingMigrationsAsync` | Relational XML | all present; `MigrateAsync` summary: "Will create the database if it does not already exist." |
| `UseNpgsql(string)` | Npgsql EF XML | `M:…NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql\`\`1(…DbContextOptionsBuilder{\`\`0},System.String,System.Action{…})` (the action parameter is optional; N0's probe calls `UseNpgsql("…")`) |
| `PostgresException.SqlState` / `ConstraintName` | Npgsql XML; reflection | `P:Npgsql.PostgresException.SqlState`, `P:Npgsql.PostgresException.ConstraintName`; base `Npgsql.NpgsqlException` |
| `PostgresErrorCodes.CheckViolation/UniqueViolation/ForeignKeyViolation` | reflection (fields not in XML) | `23514`, `23505`, `23503` (match the spec's SqlStates) |
| `NpgsqlConnectionStringBuilder(string)`, `.Database` | Npgsql XML | `M:Npgsql.NpgsqlConnectionStringBuilder.#ctor(System.String)`, `P:Npgsql.NpgsqlConnectionStringBuilder.Database` |
| `PostgreSqlBuilder(string)`; parameterless constructor obsolete | Testcontainers.PostgreSql XML; `grep -a` on the DLL | XML: `#ctor`, `#ctor(System.String)` ("The full Docker image name …"), `#ctor(…IImage)`; DLL string: "This parameterless constructor is obsolete and will be removed. Use the constructor with the image parameter instead". **Constructor reflection failed** (`Could not load file or assembly 'Docker.DotNet, Version=4.3.0.0'`), so the obsolete flag comes from the DLL string and not from reflection. N1 report row 12 says reflection showed it; I could not reproduce that step. The spec's conclusion (`new PostgreSqlBuilder("postgres:17-alpine")`) still stands on two sources. |
| `PostgreSqlContainer.GetConnectionString()`; implemented interfaces | Testcontainers.PostgreSql XML; reflection | `M:…PostgreSqlContainer.GetConnectionString`; interfaces: `IAsyncDisposable, IConnectionStringProvider, IContainer, IDatabaseContainer` (**no `IDisposable`**) |
| `IAsyncLifetime`, `ICollectionFixture\`1`, `CollectionDefinitionAttribute(string)`, `CollectionAttribute(string)`, `TraitAttribute(string,string)`, `Assert.ThrowsAsync<T>(Func<Task>)` | `$P/xunit.extensibility.core/2.9.3/lib/netstandard1.1/xunit.core.xml`, `xunit.assert` XML | all present |
| `Guid.CreateVersion7()`, `TimeProvider`, `TimeProvider.System`, `TimeProvider.GetUtcNow()`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull(object,string)`, `Enum.IsDefined<T>(T)`, `DateTimeOffset.ToUniversalTime`, `ArgumentOutOfRangeException(string,object,string)`, `KeyNotFoundException(string)` | ref pack `System.Runtime.xml` | all present, e.g. `M:System.Guid.CreateVersion7`, `M:System.TimeProvider.GetUtcNow`, `M:System.Enum.IsDefined\`\`1(\`\`0)` |
| `dotnet ef` flags `--no-connect`, `--connection`, `--startup-project`, `--output-dir` default | `dotnet ef … --help` (dotnet-ef `10.0.12`) | `--no-connect Don't connect to the database.`; `--connection <CONNECTION> The connection string to the database. Defaults to the one specified in AddDbContext or OnConfiguring.`; `-s\|--startup-project … Defaults to the current working directory.`; `-o\|--output-dir … Defaults to "Migrations".` |
| Analyzer severities the spec relies on | `…/sdk/10.0.401/Sdks/Microsoft.NET.Sdk/analyzers/build/config/analysislevel_10_recommended.globalconfig` | warning: CA1510, CA2208, CA2201, CA1710, CA1711, CA1716, CA1720, CA1805, CA1822, CA1852, CA2016, CA1001, CA1305, CA1304, CA1311, CA1051, CA1859, CA1861, CA1816, CA2263. Absent: **CA1308** (so `ToLowerInvariant()` is safe), CA1849 (so `Tasks.Add` is safe), CA1032, CA1515, CA2007, CA1062, CA1002, CA1812 |
| `.editorconfig` build-relevant severities | `grep -nE ':(warning\|error)\|naming_rule.*severity'` | only `dotnet_style_readonly_field = true:warning` and `csharp_prefer_static_local_function = true:warning`; every naming rule is `suggestion`; `[tests/**.cs]` CA1707 none; `[src/**/Migrations/*.cs]` `generated_code = true` |

**N1 `[uncertain]` items, after this review:**
- #1 and #2: orch reports them resolved. I did not re-run them (F3).
- #3 (FK index), #4 (`ORDER BY` nulls), #5 (`ExecuteSqlRawAsync` unwrapped), #9 (status strings in `InsertData`,
  insert order) and #10 (`varchar(20)`): still open, owned by B1/B3 as the spec says.
- #6 (CA1001): narrowed but still `[uncertain]`. CA1001 is `warning`, and `PostgreSqlContainer` implements
  `IAsyncDisposable` but not `IDisposable`. It fires on B3's fixture only if CA1001 counts an `IAsyncDisposable`-only
  field.
- #7 and #8: moot. The spec aliases the column `"Value"`, and it rejected the `lower()` CHECK.

## Decisions for the human

Neutral list of choices the brief leaves open, or where the spec goes beyond it.

1. **Unlisted package.** `Microsoft.EntityFrameworkCore.Relational` 10.0.12 is an explicit reference in
   Infrastructure (HARD RULE 7). Npgsql EF 10.0.3 needs `[10.0.4, 11.0.0)`, while Design 10.0.12 (a development
   dependency) builds against 10.0.12. The spec has no alternative if the pin is rejected.
2. **E-mail uniqueness.** It ignores case only because the domain lowercases e-mails (`ToLowerInvariant()`) before
   they reach a plain unique index. The database itself enforces exact-match only, so raw SQL can insert a mixed-case
   duplicate. The alternative is an ICU nondeterministic collation (ADR 0005).
3. **Assignment.**
   - It is set once at creation.
   - There is no reassign operation and no assignment history.
   - An unassigned task cannot exist (ADR 0002).
4. **Transition rule.**
   - Any change out of `New` or `InProgress` is allowed, including `New → Completed`, `New → Cancelled` and
     `InProgress → New`.
   - A change to the same non-final status is a silent no-op.
   - `Completed → Completed` and `Cancelled → Cancelled` are rejected as BR4.
5. **BR3 scope.** Only the assignee's `IsActive` is checked. An inactive employee can still create tasks, and tasks
   they held before deactivation stay valid and can still change status.
6. **Precedence differs.** The domain checks BR5 before BR3; the service checks BR3 before calling the domain. If
   creator == assignee and that employee is inactive, the service reports BR3 and the domain alone would report BR5.
   No test covers this case.
7. **Deadlines.** `PlannedStartAt` and `DueAt` are both nullable, so a task may have no deadline. BR2 applies only
   when both are set, and equal values are allowed.
8. **Non-UTC offsets.** They are normalised with `ToUniversalTime()`, not rejected. "Now" comes from `TimeProvider`
   in the service (ADR 0006).
9. **Beyond the brief's list of DB objects.** Both are justified in the spec:
   - `ck_tasks_status_valid`: a fourth CHECK, restricting the text status values.
   - `ix_tasks_creator_id`: a fourth index, which EF would create for the FK anyway.
10. **BR3 and BR4 have no DB constraint.**
    - An `xmin` optimistic concurrency token on `tasks` protects BR4 against lost updates.
    - Raw SQL can still bypass both rules.
    - A deactivation that commits between the service's read and its insert is not detected (ADR 0004).
11. **BR1 has no throwing domain guard.** The domain enforces it structurally, through the `CompletedAt` assignment in
    `ChangeStatus`. FA's red evidence deletes that assignment.
12. **Both FKs are `RESTRICT`.** Deleting an employee who has tasks fails, because employees are meant to be
    deactivated, not deleted.
13. **Unknown ids.** `CreateTaskAsync` and `ChangeTaskStatusAsync` throw `KeyNotFoundException`.
    `ListTasksByAssigneeAsync` returns an empty list.
14. **Keys, status storage and the exception type.**
    - Keys are domain-generated UUIDv7 values (ADR 0003).
    - The status is stored as text in `varchar(20)`, not as an int or a PostgreSQL enum (ADR 0007).
    - There is one custom `BusinessRuleViolationException` with `RuleId` (ADR 0008).
15. **Layout.** There are two test projects: the unit project references Domain only. Application references
    Infrastructure directly, with no repository and no interface (ADR 0001).
16. **Employees.** No use case creates or deactivates employees: they come only from the seed. `Employee.Create` and
    `Deactivate()` exist for the domain and the tests.

## Commands run (N2)

All of these are read-only against the repo. Scratch scripts are in my session scratchpad (`n2/reflect.ps1`,
`n2/reflect2.ps1`).

1. `git status --short; git log --oneline -5; git branch --show-current`. The branch is `feature/task-management`,
   HEAD is `d46fbef`, ` M .wiki/index.md` and ` M .wiki/plan/graph.yaml` were already present, and N1's files were
   untracked.
2. `git diff .wiki/plan/graph.yaml .wiki/index.md`: orch filled the exact outputs from spec §1, set N0/N1 to `done`,
   N2 to `in_progress`, and removed `[[graph]]` from the index's `related`.
3. Read `ORCHESTRATOR_PROMPT.md`, `AGENTS.md`, `Directory.Build.props`, spec, both entity pages, the 5 BR pages,
   `decisions/index.md`, `index.md`, `log.md`, `graph.yaml`, ADRs 0001–0008, `orch-to-N1-001.md`, and N1's report
   (last).
4. The `.editorconfig` severity greps and the recommended-globalconfig grep (rows above).
5. The package folder, nuspec and XML-doc greps (rows above).
6. `pwsh reflect.ps1`: printed every EF, Relational, Npgsql and TimeProvider result above, then exited 1 at the
   Testcontainers block (`You cannot call a method on a null-valued expression`). `reflect2.ps1` hit a loader
   exception (`Docker.DotNet 4.3.0.0` not found) but still returned the `PostgreSqlContainer` interfaces.
7. `grep -a` on `Testcontainers.PostgreSql.dll` for the obsolete message.
8. `dotnet ef --version` and `dotnet ef migrations list|database update|migrations add --help`.
9. `grep -n xmin` on the N0 probe migration (`<scratchpad>/probe/src/Probe.Infra/Migrations/*_Initial.cs`, read only).
10. The name and test-name consistency scripts over `.wiki/domain/*.md`, `docs/adr/*.md` and `graph.yaml` (findings
    F6 and check 4).

## git status --short (end of review)

```
 M .wiki/index.md
 M .wiki/plan/graph.yaml
?? .wiki/agents/bus/N1-to-orch-001.md
?? .wiki/agents/bus/N2-to-orch-001.md
?? .wiki/agents/bus/orch-to-N1-001.md
?? .wiki/agents/bus/orch-to-N2-001.md
?? .wiki/decisions/index.md
?? .wiki/domain/br1-completed-at.md
?? .wiki/domain/br2-due-not-before-start.md
?? .wiki/domain/br3-inactive-assignee.md
?? .wiki/domain/br4-final-statuses.md
?? .wiki/domain/br5-no-self-assignment.md
?? .wiki/domain/employee.md
?? .wiki/domain/spec.md
?? .wiki/domain/task-item.md
?? docs/adr/0001-solution-layout.md
?? docs/adr/0002-assignee-column-on-task.md
?? docs/adr/0003-domain-generated-uuid-keys.md
?? docs/adr/0004-no-db-constraint-for-br3-br4.md
?? docs/adr/0005-email-uniqueness-lowercase.md
?? docs/adr/0006-utc-normalisation-and-explicit-time.md
?? docs/adr/0007-status-as-text-and-transition-rule.md
?? docs/adr/0008-business-rule-violation-exception.md
```

The only new line compared with the start is `?? .wiki/agents/bus/N2-to-orch-001.md`.
