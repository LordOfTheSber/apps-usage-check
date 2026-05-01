using UsageTracker.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UsageTracker.Infrastructure.Data.Configurations;

public sealed class TimeAdjustmentConfiguration : IEntityTypeConfiguration<TimeAdjustment>
{
    public void Configure(EntityTypeBuilder<TimeAdjustment> builder)
    {
        builder.ToTable("time_adjustments");

        builder.HasKey(adjustment => adjustment.Id);

        builder.Property(adjustment => adjustment.Id)
            .ValueGeneratedNever();

        builder.Property(adjustment => adjustment.TrackedProcessId)
            .IsRequired();

        builder.Property(adjustment => adjustment.AdjustmentType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(adjustment => adjustment.AdjustmentSeconds)
            .IsRequired();

        builder.Property(adjustment => adjustment.Reason)
            .HasMaxLength(2048);

        builder.Property(adjustment => adjustment.AppliedAt)
            .IsRequired();

        builder.HasIndex(adjustment => adjustment.TrackedProcessId);
    }
}
