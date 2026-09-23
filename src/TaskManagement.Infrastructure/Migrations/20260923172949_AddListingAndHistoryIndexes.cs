// AddListingAndHistoryIndexes: indexes only, no table or column changes.
//
// Indexes
//   + ix_tasks_assignee_id_due_at_id      tasks(assignee_id, due_at, id): serves the keyset listing
//     (WHERE assignee_id = @a ORDER BY due_at, id). Without it PostgreSQL scanned ix_tasks_due_at and sorted
//     the assignee's whole task set for every page.
//   + ix_task_history_task_id_changed_at_id  task_history(task_id, changed_at, id): serves the history read,
//     whose order now breaks ties on id.
//   - ix_task_history_task_id_changed_at   replaced by the line above.
﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingAndHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_task_history_task_id_changed_at",
                table: "task_history");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_assignee_id_due_at_id",
                table: "tasks",
                columns: new[] { "assignee_id", "due_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_task_history_task_id_changed_at_id",
                table: "task_history",
                columns: new[] { "task_id", "changed_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tasks_assignee_id_due_at_id",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "ix_task_history_task_id_changed_at_id",
                table: "task_history");

            migrationBuilder.CreateIndex(
                name: "ix_task_history_task_id_changed_at",
                table: "task_history",
                columns: new[] { "task_id", "changed_at" });
        }
    }
}
