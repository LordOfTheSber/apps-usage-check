namespace AppsUsageCheck.Core.Models;

public sealed class TrackedProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProcessName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool IsPaused { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
