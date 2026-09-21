using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain;

namespace TaskManagement.Infrastructure;

/// <summary>
/// The EF Core session over the Task Management database: employees and their tasks.
/// </summary>
/// <remarks>
/// The mapping, constraints, indexes and seed live in the <see cref="IEntityTypeConfiguration{TEntity}"/> classes
/// of the <c>TaskManagement.Infrastructure.Configurations</c> namespace. BR1, BR2 and BR5 are CHECK constraints;
/// BR3 and BR4 are enforced by the trigger <c>trg_tasks_br3_br4</c>, created by the <c>InitialCreate</c> migration.
/// </remarks>
public sealed class TaskManagementDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskManagementDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context, including the Npgsql provider and connection string.</param>
    public TaskManagementDbContext(DbContextOptions<TaskManagementDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets the employees (table <c>employees</c>).</summary>
    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>Gets the tasks (table <c>tasks</c>).</summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>Task change history.</summary>
    public DbSet<TaskHistoryEntry> TaskHistory => Set<TaskHistoryEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskManagementDbContext).Assembly);
    }
}
