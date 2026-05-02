using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UsageTracker.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260502000000_AddIconExtractedAt")]
public partial class AddIconExtractedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "icon_extracted_at",
            table: "tracked_processes",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "icon_extracted_at",
            table: "tracked_processes");
    }
}
