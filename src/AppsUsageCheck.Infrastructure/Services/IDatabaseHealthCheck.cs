namespace AppsUsageCheck.Infrastructure.Services;

public interface IDatabaseHealthCheck
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    DatabaseConnectionState ConnectionState { get; }

    bool IsConnected { get; }

    string StatusText { get; }

    DateTimeOffset? LastCheckedAtUtc { get; }
}
