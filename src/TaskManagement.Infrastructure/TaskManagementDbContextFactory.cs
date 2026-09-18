using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManagement.Infrastructure;

/// <summary>
/// Builds a <see cref="TaskManagementDbContext"/> for the <c>dotnet ef</c> design-time tools.
/// </summary>
/// <remarks>
/// The connection string holds no secret and is only a placeholder for model building: <c>migrations add</c> and
/// <c>migrations list --no-connect</c> never connect. Commands that touch a real database pass
/// <c>--connection "&lt;connection string&gt;"</c>.
/// </remarks>
public sealed class TaskManagementDbContextFactory : IDesignTimeDbContextFactory<TaskManagementDbContext>
{
    /// <inheritdoc />
    public TaskManagementDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TaskManagementDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=task_management;Username=postgres")
            .Options);
}
