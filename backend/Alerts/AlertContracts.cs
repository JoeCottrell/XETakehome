namespace RateAlerts.Api.Alerts;

/// <summary>
/// The create payload as it arrives. Every field is nullable so that a missing or malformed value
/// becomes a 400 with a message the UI can show, rather than a model-binding error or a silent zero.
/// </summary>
public sealed record CreateAlertRequest(string? Pair, decimal? Threshold, string? Direction);

/// <summary>
/// The wire shape of an alert. It keeps the fields the original stub exposed - id, pair, threshold,
/// direction, triggered - so anything already built against that contract keeps working, and adds
/// the context the UI needs to explain itself: the current rate, and when and at what the alert fired.
/// </summary>
public sealed record AlertResponse(
    Guid Id,
    string Pair,
    decimal Threshold,
    string Direction,
    bool Triggered,
    decimal? CurrentRate,
    DateTimeOffset? RateAsOf,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TriggeredAt,
    decimal? TriggeredRate)
{
    /// <summary>Rates are shown to four decimal places, as the rate board always has.</summary>
    private const int DisplayDecimals = 4;

    public static AlertResponse From(AlertView view)
    {
        var alert = view.Alert;

        return new AlertResponse(
            alert.Id,
            alert.Pair.ToString(),
            alert.Threshold,
            alert.Direction.ToWireValue(),
            alert.Triggered,
            view.Rate is null ? null : Math.Round(view.Rate.Mid, DisplayDecimals),
            view.Rate?.AsOf,
            alert.CreatedAt,
            alert.TriggeredAt,
            alert.TriggeredRate is null ? null : Math.Round(alert.TriggeredRate.Value, DisplayDecimals));
    }
}
