using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Alerts;

/// <summary>
/// The core rule: does this rate fire this alert? Deliberately a pure static function with no
/// storage, clock or I/O of its own, so it can be tested exhaustively.
/// </summary>
public static class AlertEvaluator
{
    /// <summary>
    /// Returns the alert as it stands once <paramref name="rate"/> has been taken into account:
    /// unchanged if it does not fire or has already fired, otherwise stamped as triggered.
    /// </summary>
    /// <param name="observedAt">The time the rate was observed, used as the trigger time.</param>
    public static Alert Evaluate(Alert alert, Rate rate, DateTimeOffset observedAt)
    {
        if (rate.Pair != alert.Pair)
        {
            throw new ArgumentException(
                $"Rate for {rate.Pair} cannot be used to evaluate an alert on {alert.Pair}.",
                nameof(rate));
        }

        // Already fired: latched, so there is nothing to decide.
        if (alert.Triggered)
        {
            return alert;
        }

        if (!CrossesThreshold(alert.Direction, alert.Threshold, rate.Mid))
        {
            return alert;
        }

        return alert with { TriggeredAt = observedAt, TriggeredRate = rate.Mid };
    }

    /// <summary>
    /// Whether a rate is on the firing side of the threshold. The comparison is strict: a rate that
    /// is exactly on the threshold has not gone above or below it, so it does not fire.
    /// </summary>
    public static bool CrossesThreshold(AlertDirection direction, decimal threshold, decimal rate) =>
        direction switch
        {
            AlertDirection.Above => rate > threshold,
            AlertDirection.Below => rate < threshold,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown alert direction."),
        };
}
