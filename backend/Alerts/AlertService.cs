using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Alerts;

/// <summary>An alert plus the rate it was last evaluated against, if one was available.</summary>
public sealed record AlertView(Alert Alert, Rate? Rate);

/// <summary>The validated shape of a create request, once the controller has checked the input.</summary>
public sealed record NewAlert(CurrencyPair Pair, decimal Threshold, AlertDirection Direction);

public interface IAlertService
{
    /// <summary>Evaluates every alert against current rates and returns them, oldest first.</summary>
    Task<IReadOnlyList<AlertView>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Evaluates a single alert, or returns null if the id is unknown.</summary>
    Task<AlertView?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<AlertView> CreateAsync(NewAlert request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Ties the store, the rate provider and the evaluation rule together.
/// </summary>
/// <remarks>
/// Alerts are evaluated when they are read rather than by a background poller. For a UI that only
/// shows state when somebody is looking, that is enough, and it keeps upstream calls proportional
/// to use instead of running all day against a metered API. It does mean a crossing that happens
/// and reverses between two reads is missed - the moment alerts need to push a notification, this
/// has to become a scheduled evaluation, which is why the triggered state is stored on the alert
/// rather than computed in the response.
/// </remarks>
public sealed class AlertService : IAlertService
{
    private readonly IAlertStore _store;
    private readonly IRateProvider _rateProvider;
    private readonly TimeProvider _timeProvider;

    public AlertService(IAlertStore store, IRateProvider rateProvider, TimeProvider timeProvider)
    {
        _store = store;
        _rateProvider = rateProvider;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AlertView>> ListAsync(CancellationToken cancellationToken)
    {
        var alerts = await _store.ListAsync(cancellationToken);

        if (alerts.Count == 0)
        {
            return [];
        }

        var rates = await _rateProvider.GetRatesAsync(
            alerts.Select(alert => alert.Pair).Distinct().ToArray(),
            cancellationToken);

        var views = new List<AlertView>(alerts.Count);

        foreach (var alert in alerts)
        {
            if (!rates.TryGetValue(alert.Pair, out var rate))
            {
                // No rate right now (unknown pair, or the API is down): leave the alert as it was.
                views.Add(new AlertView(alert, null));
                continue;
            }

            views.Add(new AlertView(await EvaluateAsync(alert, rate, cancellationToken), rate));
        }

        return views;
    }

    public async Task<AlertView?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var alert = await _store.FindAsync(id, cancellationToken);

        if (alert is null)
        {
            return null;
        }

        var rates = await _rateProvider.GetRatesAsync([alert.Pair], cancellationToken);

        return rates.TryGetValue(alert.Pair, out var rate)
            ? new AlertView(await EvaluateAsync(alert, rate, cancellationToken), rate)
            : new AlertView(alert, null);
    }

    public async Task<AlertView> CreateAsync(NewAlert request, CancellationToken cancellationToken)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Pair = request.Pair,
            Threshold = request.Threshold,
            Direction = request.Direction,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        var rates = await _rateProvider.GetRatesAsync([request.Pair], cancellationToken);

        // Evaluate before storing, so an alert created on the wrong side of the current rate comes
        // back triggered instead of looking inert until the next list call.
        Rate? rate = rates.TryGetValue(request.Pair, out var current) ? current : null;
        if (rate is not null)
        {
            alert = AlertEvaluator.Evaluate(alert, rate, _timeProvider.GetUtcNow());
        }

        await _store.AddAsync(alert, cancellationToken);
        return new AlertView(alert, rate);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        _store.DeleteAsync(id, cancellationToken);

    private async Task<Alert> EvaluateAsync(Alert alert, Rate rate, CancellationToken cancellationToken)
    {
        var evaluated = AlertEvaluator.Evaluate(alert, rate, _timeProvider.GetUtcNow());

        if (evaluated == alert)
        {
            return alert;
        }

        // If the alert was deleted or already stamped by a concurrent request, that result wins and
        // we report what is actually stored.
        return await _store.TryReplaceAsync(alert, evaluated, cancellationToken)
            ? evaluated
            : await _store.FindAsync(alert.Id, cancellationToken) ?? evaluated;
    }
}
