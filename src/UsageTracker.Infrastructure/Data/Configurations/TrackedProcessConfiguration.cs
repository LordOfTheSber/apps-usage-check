using UsageTracker.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UsageTracker.Infrastructure.Data.Configurations;

public sealed class TrackedProcessConfiguration : IEntityTypeConfiguration<TrackedProcess>
{
    public void Configure(EntityTypeBuilder<TrackedProcess> builder)
    {
        builder.ToTable("tracked_processes");

        builder.HasKey(trackedProcess => trackedProcess.Id);

        builder.Property(trackedProcess => trackedProcess.Id)
            .ValueGeneratedNever();

        builder.Property(trackedProcess => trackedProcess.ProcessName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(trackedProcess => trackedProcess.DisplayName)
            .HasMaxLength(256);

        builder.Property(trackedProcess => trackedProcess.IsPaused)
            .IsRequired();

        builder.Property(trackedProcess => trackedProcess.CreatedAt)
            .IsRequired();

        builder.Property(trackedProcess => trackedProcess.UpdatedAt)
            .IsRequired();

        builder.HasIndex(trackedProcess => trackedProcess.ProcessName)
            .IsUnique();

        builder.HasMany<UsageSession>()
            .WithOne()
            .HasForeignKey(session => session.TrackedProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<TimeAdjustment>()
            .WithOne()
            .HasForeignKey(adjustment => adjustment.TrackedProcessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
