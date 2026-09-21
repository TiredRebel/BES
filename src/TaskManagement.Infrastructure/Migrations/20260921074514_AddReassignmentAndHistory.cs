// AddReassignmentAndHistory: schema changes
//
// Tables and columns
//   task_history: id uuid NOT NULL, task_id uuid NOT NULL, changed_by_id uuid NOT NULL,
//                 changed_at timestamptz NOT NULL, change_type character varying(20) NOT NULL,
//                 old_value character varying(200) NOT NULL, new_value character varying(200) NOT NULL
// Primary keys
//   pk_task_history (task_history.id)
// Foreign keys
//   fk_task_history_tasks_task_id           task_history(task_id)       -> tasks(id) ON DELETE RESTRICT
//   fk_task_history_employees_changed_by_id task_history(changed_by_id) -> employees(id) ON DELETE RESTRICT
// Indexes
//   IX_task_history_changed_by_id       task_history(changed_by_id)
//   ix_task_history_task_id_changed_at  task_history(task_id, changed_at)
// Trigger update (raw SQL in Up(), reverted in Down())
//   function tasks_enforce_br3_br4() updated to check BR3 and BR4 on UPDATE OF assignee_id
//   trigger trg_tasks_br3_br4 recreated with BEFORE INSERT OR UPDATE OF status, assignee_id ON tasks
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReassignmentAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    change_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    old_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    new_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_history_employees_changed_by_id",
                        column: x => x.changed_by_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_history_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_history_changed_by_id",
                table: "task_history",
                column: "changed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_history_task_id_changed_at",
                table: "task_history",
                columns: new[] { "task_id", "changed_at" });

            // Extend BR3 and BR4 enforcement to cover assignee_id updates (ADR 0009 / ADR 0010).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION tasks_enforce_br3_br4() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    assignee_active boolean;
                BEGIN
                    IF TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.assignee_id IS DISTINCT FROM OLD.assignee_id) THEN
                        -- BR3: the assignee must be active. FOR SHARE blocks a concurrent deactivation until this transaction ends.
                        -- An unknown assignee leaves assignee_active NULL, so the FK reports it (23503), not BR3.
                        SELECT is_active INTO assignee_active FROM employees WHERE id = NEW.assignee_id FOR SHARE;
                        IF assignee_active IS FALSE THEN
                            RAISE EXCEPTION 'BR3: employee % is inactive and cannot be given a new task.', NEW.assignee_id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br3_assignee_active';
                        END IF;
                    END IF;

                    IF TG_OP = 'UPDATE' AND OLD.status IN ('Completed', 'Cancelled') THEN
                        IF NEW.status IS DISTINCT FROM OLD.status OR NEW.assignee_id IS DISTINCT FROM OLD.assignee_id THEN
                            -- BR4: Completed and Cancelled are final.
                            RAISE EXCEPTION 'BR4: task % is %; Completed and Cancelled are final.', OLD.id, OLD.status
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br4_final_status';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_tasks_br3_br4 ON tasks;
                CREATE TRIGGER trg_tasks_br3_br4
                    BEFORE INSERT OR UPDATE OF status, assignee_id ON tasks
                    FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert trigger and function to InitialCreate definition
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION tasks_enforce_br3_br4() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    assignee_active boolean;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        SELECT is_active INTO assignee_active FROM employees WHERE id = NEW.assignee_id FOR SHARE;
                        IF assignee_active IS FALSE THEN
                            RAISE EXCEPTION 'BR3: employee % is inactive and cannot be given a new task.', NEW.assignee_id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br3_assignee_active';
                        END IF;
                    ELSIF OLD.status IN ('Completed', 'Cancelled') AND NEW.status IS DISTINCT FROM OLD.status THEN
                        RAISE EXCEPTION 'BR4: task % is %; Completed and Cancelled are final.', OLD.id, OLD.status
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br4_final_status';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_tasks_br3_br4 ON tasks;
                CREATE TRIGGER trg_tasks_br3_br4
                    BEFORE INSERT OR UPDATE OF status ON tasks
                    FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4();
                """);

            migrationBuilder.DropTable(
                name: "task_history");
        }
    }
}
