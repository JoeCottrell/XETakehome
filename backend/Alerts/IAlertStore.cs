namespace RateAlerts.Api.Alerts;

/// <summary>
/// Storage for alerts.
/// </summary>
/// <remarks>
/// The methods are asynchronous even though the only implementation today is a dictionary. The
/// point of the interface is that a database-backed store can be dropped in without touching the
/// service or the controller, and every such store would be async.
/// </remarks>
public interface IAlertStore
{
    /// <summary>All alerts, oldest first.</summary>
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken);

    Task<Alert?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Alert alert, CancellationToken cancellationToken);

    /// <returns>True if an alert was removed, false if the id was unknown.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces <paramref name="previous"/> with <paramref name="updated"/> only if the stored alert
    /// is still exactly <paramref name="previous"/>. Concurrent evaluations therefore cannot
    /// resurrect a deleted alert or overwrite each other's trigger stamp.
    /// </summary>
    Task<bool> TryReplaceAsync(Alert previous, Alert updated, CancellationToken cancellationToken);
}
