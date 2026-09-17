using System.Collections.Concurrent;

namespace RateAlerts.Api.Alerts;

/// <summary>
/// Keeps alerts in process memory for the lifetime of the server.
/// </summary>
/// <remarks>
/// Chosen deliberately: nothing in this app needs alerts to survive a restart, there is no user
/// identity to scope them to yet, and a real store would drag in a database, migrations and a
/// connection string for no gain over a couple of hours. The cost is that everything is lost on
/// restart and that a second instance would not see the first instance's alerts, which rules the
/// design out for anything but a single-node demo. NOTES.md covers what replacing it would take.
/// </remarks>
public sealed class InMemoryAlertStore : IAlertStore
{
    private readonly ConcurrentDictionary<Guid, Alert> _alerts = new();

    public Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Alert> alerts = _alerts.Values
            .OrderBy(alert => alert.CreatedAt)
            .ThenBy(alert => alert.Id)
            .ToList();

        return Task.FromResult(alerts);
    }

    public Task<Alert?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_alerts.TryGetValue(id, out var alert) ? alert : null);

    public Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        _alerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_alerts.TryRemove(id, out _));

    public Task<bool> TryReplaceAsync(Alert previous, Alert updated, CancellationToken cancellationToken) =>
        Task.FromResult(_alerts.TryUpdate(updated.Id, updated, previous));
}
