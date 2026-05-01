using UsageTracker.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UsageTracker.Infrastructure.Data.Configurations;

public sealed class UsageSessionConfiguration : IEntityTypeConfiguration<UsageSession>
{
    public void Configure(EntityTypeBuilder<UsageSession> builder)
    {
        builder.ToTable("usage_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .ValueGeneratedNever();

        builder.Property(session => session.TrackedProcessId)
            .IsRequired();

        builder.Property(session => session.SessionStart)
            .IsRequired();

        builder.Property(session => session.TotalRunningSeconds)
            .IsRequired();

        builder.Property(session => session.ForegroundSeconds)
            .IsRequired();

        builder.Property(session => session.IsManualEdit)
            .IsRequired();

        builder.Property(session => session.Notes)
            .HasMaxLength(2048);

        builder.Property(session => session.CreatedAt)
            .IsRequired();

        builder.Property(session => session.UpdatedAt)
            .IsRequired();

        builder.HasIndex(session => session.TrackedProcessId);

        builder.HasIndex(session => new { session.TrackedProcessId, session.SessionEnd });

        builder.HasIndex(session => new { session.TrackedProcessId, session.SessionStart });
    }
}
