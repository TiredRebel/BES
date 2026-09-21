#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain;

namespace TaskManagement.Infrastructure.Configurations;

public sealed class TaskHistoryEntryConfiguration : IEntityTypeConfiguration<TaskHistoryEntry>
{
    public void Configure(EntityTypeBuilder<TaskHistoryEntry> builder)
    {
        builder.ToTable("task_history");

        builder.HasKey(h => h.Id).HasName("pk_task_history");
        builder.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(h => h.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(h => h.ChangedById).HasColumnName("changed_by_id").IsRequired();
        builder.Property(h => h.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(h => h.ChangeType).HasColumnName("change_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.OldValue).HasColumnName("old_value").HasMaxLength(TaskHistoryEntry.ValueMaxLength).IsRequired();
        builder.Property(h => h.NewValue).HasColumnName("new_value").HasMaxLength(TaskHistoryEntry.ValueMaxLength).IsRequired();

        builder.HasOne<TaskItem>().WithMany().HasForeignKey(h => h.TaskId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_task_history_tasks_task_id");
        builder.HasOne<Employee>().WithMany().HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_task_history_employees_changed_by_id");

        builder.HasIndex(h => new { h.TaskId, h.ChangedAt }).HasDatabaseName("ix_task_history_task_id_changed_at");
    }
}
