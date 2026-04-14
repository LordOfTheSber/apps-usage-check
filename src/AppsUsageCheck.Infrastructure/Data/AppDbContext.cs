using AppsUsageCheck.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AppsUsageCheck.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TrackedProcess> TrackedProcesses => Set<TrackedProcess>();

    public DbSet<UsageSession> UsageSessions => Set<UsageSession>();

    public DbSet<TimeAdjustment> TimeAdjustments => Set<TimeAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
