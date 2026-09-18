# 0001. Solution layout: three source projects, two test projects

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

The brief suggests Domain / Infrastructure / Application / Tests and asks to justify each project or merge them.
Workers build in parallel worktrees: A1 (domain) beside A2 (unit tests), then B1 (persistence) beside B2 (service)
beside B3 (integration tests). The build treats warnings as errors and requires XML docs on every public member.
No repositories, CQRS or DI host are in scope.

## Decision

- `src/TaskManagement.Domain`: entities, enum, one domain exception. No package references, so it cannot pick up
  EF attributes and its tests need nothing but xUnit.
- `src/TaskManagement.Infrastructure`: `TaskManagementDbContext`, Fluent configurations, seed, migrations and the
  design-time factory. The only project with EF Core / Npgsql packages and the only target of `dotnet ef`.
- `src/TaskManagement.Application`: `TaskService` with the three use cases. It references Infrastructure directly
  and uses the DbContext as its unit of work: no repository and no interface, because there is one implementation
  and the tests hit a real database. Kept separate from Infrastructure so B1 and B2 own disjoint directories and
  the use-case surface is one small assembly.
- `tests/TaskManagement.UnitTests` (references Domain) and `tests/TaskManagement.IntegrationTests` (references
  Domain, Infrastructure, Application; adds Testcontainers). Two projects so unit tests build and run without EF
  Core or Docker, which is also what fan-in A needs before B1 exists.
- Package references per project are listed in the spec (§1). `Microsoft.EntityFrameworkCore.Relational` 10.0.12
  is referenced explicitly in Infrastructure (approved by the human at N2): Npgsql 10.0.3 only requires `>= 10.0.4`, and
  without the pin consumers of Infrastructure fail with MSB3277 (proven by the N0 probe).

## Consequences

- Five csproj files and one `.slnx`. Merging Application into Infrastructure would save one file but would mix B1's
  and B2's outputs in one directory.
- Application depends on Infrastructure (not the reverse). Swapping the persistence technology would touch the
  service, which is acceptable for a data-layer module with one database.
- `dotnet test --filter Category=Unit` runs across both test projects; the integration project contributes zero
  tests to it. VSTest prints "No test matches" for that project and still exits 0 (verified on the N0 probe).
