using System;
using UsageTracker.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace UsageTracker.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity<TimeAdjustment>(
            entity =>
            {
                entity.Property(adjustment => adjustment.Id)
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                entity.Property(adjustment => adjustment.AdjustmentSeconds)
                    .HasColumnType("bigint")
                    .HasColumnName("adjustment_seconds");

                entity.Property(adjustment => adjustment.AdjustmentType)
                    .IsRequired()
                    .HasMaxLength(64)
                    .HasColumnType("character varying(64)")
                    .HasColumnName("adjustment_type");

                entity.Property(adjustment => adjustment.AppliedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("applied_at");

                entity.Property(adjustment => adjustment.Reason)
                    .HasMaxLength(2048)
                    .HasColumnType("character varying(2048)")
                    .HasColumnName("reason");

                entity.Property(adjustment => adjustment.TrackedProcessId)
                    .HasColumnType("uuid")
                    .HasColumnName("tracked_process_id");

                entity.HasKey(adjustment => adjustment.Id)
                    .HasName("pk_time_adjustments");

                entity.HasIndex(adjustment => adjustment.TrackedProcessId)
                    .HasDatabaseName("ix_time_adjustments_tracked_process_id");

                entity.ToTable("time_adjustments");
            });

        modelBuilder.Entity<TrackedProcess>(
            entity =>
            {
                entity.Property(trackedProcess => trackedProcess.Id)
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                entity.Property(trackedProcess => trackedProcess.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                entity.Property(trackedProcess => trackedProcess.DisplayName)
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("display_name");

                entity.Property(trackedProcess => trackedProcess.IsPaused)
                    .HasColumnType("boolean")
                    .HasColumnName("is_paused");

                entity.Property(trackedProcess => trackedProcess.ProcessName)
                    .IsRequired()
                    .HasMaxLength(256)
                    .HasColumnType("character varying(256)")
                    .HasColumnName("process_name");

                entity.Property(trackedProcess => trackedProcess.UpdatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                entity.HasKey(trackedProcess => trackedProcess.Id)
                    .HasName("pk_tracked_processes");

                entity.HasIndex(trackedProcess => trackedProcess.ProcessName)
                    .IsUnique()
                    .HasDatabaseName("ix_tracked_processes_process_name");

                entity.ToTable("tracked_processes");
            });

        modelBuilder.Entity<UsageSession>(
            entity =>
            {
                entity.Property(session => session.Id)
                    .ValueGeneratedNever()
                    .HasColumnType("uuid")
                    .HasColumnName("id");

                entity.Property(session => session.CreatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("created_at");

                entity.Property(session => session.ForegroundSeconds)
                    .HasColumnType("bigint")
                    .HasColumnName("foreground_seconds");

                entity.Property(session => session.IsManualEdit)
                    .HasColumnType("boolean")
                    .HasColumnName("is_manual_edit");

                entity.Property(session => session.Notes)
                    .HasMaxLength(2048)
                    .HasColumnType("character varying(2048)")
                    .HasColumnName("notes");

                entity.Property(session => session.SessionEnd)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("session_end");

                entity.Property(session => session.SessionStart)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("session_start");

                entity.Property(session => session.TotalRunningSeconds)
                    .HasColumnType("bigint")
                    .HasColumnName("total_running_seconds");

                entity.Property(session => session.TrackedProcessId)
                    .HasColumnType("uuid")
                    .HasColumnName("tracked_process_id");

                entity.Property(session => session.UpdatedAt)
                    .HasColumnType("timestamp with time zone")
                    .HasColumnName("updated_at");

                entity.HasKey(session => session.Id)
                    .HasName("pk_usage_sessions");

                entity.HasIndex(session => session.TrackedProcessId)
                    .HasDatabaseName("ix_usage_sessions_tracked_process_id");

                entity.HasIndex(session => new { session.TrackedProcessId, session.SessionEnd })
                    .HasDatabaseName("ix_usage_sessions_tracked_process_id_session_end");

                entity.HasIndex(session => new { session.TrackedProcessId, session.SessionStart })
                    .HasDatabaseName("ix_usage_sessions_tracked_process_id_session_start");

                entity.ToTable("usage_sessions");
            });

        modelBuilder.Entity<TimeAdjustment>()
            .HasOne<TrackedProcess>()
            .WithMany()
            .HasForeignKey(adjustment => adjustment.TrackedProcessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_time_adjustments_tracked_processes_tracked_process_id");

        modelBuilder.Entity<UsageSession>()
            .HasOne<TrackedProcess>()
            .WithMany()
            .HasForeignKey(session => session.TrackedProcessId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usage_sessions_tracked_processes_tracked_process_id");
#pragma warning restore 612, 618
    }
}
