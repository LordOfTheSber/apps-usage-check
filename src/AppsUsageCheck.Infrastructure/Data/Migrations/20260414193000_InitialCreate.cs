using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppsUsageCheck.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260414193000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tracked_processes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                process_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                is_paused = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tracked_processes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "time_adjustments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tracked_process_id = table.Column<Guid>(type: "uuid", nullable: false),
                adjustment_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                adjustment_seconds = table.Column<long>(type: "bigint", nullable: false),
                reason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_time_adjustments", x => x.id);
                table.ForeignKey(
                    name: "fk_time_adjustments_tracked_processes_tracked_process_id",
                    column: x => x.tracked_process_id,
                    principalTable: "tracked_processes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "usage_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tracked_process_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                session_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                total_running_seconds = table.Column<long>(type: "bigint", nullable: false),
                foreground_seconds = table.Column<long>(type: "bigint", nullable: false),
                is_manual_edit = table.Column<bool>(type: "boolean", nullable: false),
                notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_usage_sessions", x => x.id);
                table.ForeignKey(
                    name: "fk_usage_sessions_tracked_processes_tracked_process_id",
                    column: x => x.tracked_process_id,
                    principalTable: "tracked_processes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_time_adjustments_tracked_process_id",
            table: "time_adjustments",
            column: "tracked_process_id");

        migrationBuilder.CreateIndex(
            name: "ix_tracked_processes_process_name",
            table: "tracked_processes",
            column: "process_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_usage_sessions_tracked_process_id",
            table: "usage_sessions",
            column: "tracked_process_id");

        migrationBuilder.CreateIndex(
            name: "ix_usage_sessions_tracked_process_id_session_end",
            table: "usage_sessions",
            columns: new[] { "tracked_process_id", "session_end" });

        migrationBuilder.CreateIndex(
            name: "ix_usage_sessions_tracked_process_id_session_start",
            table: "usage_sessions",
            columns: new[] { "tracked_process_id", "session_start" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "time_adjustments");

        migrationBuilder.DropTable(
            name: "usage_sessions");

        migrationBuilder.DropTable(
            name: "tracked_processes");
    }
}
