using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Alerts;

/// <summary>
/// A standing instruction: "tell me when GBP/CAD goes above 1.84".
/// </summary>
/// <remarks>
/// The record is immutable and evaluation returns a new instance, which keeps the evaluation rules
/// a pure function of (alert, rate) and makes them straightforward to test.
///
/// Triggering latches: once an alert has fired we keep the rate and the time it fired at, and a
/// later move back across the threshold does not clear it. The alternative - recomputing
/// "triggered" from the live rate on every read, as the stub did - loses the event the user
/// actually asked about the moment the rate moves back.
/// </remarks>
public sealed record Alert
{
    public required Guid Id { get; init; }

    public required CurrencyPair Pair { get; init; }

    public required decimal Threshold { get; init; }

    public required AlertDirection Direction { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the alert first fired, or null while it is still waiting.</summary>
    public DateTimeOffset? TriggeredAt { get; init; }

    /// <summary>The rate that fired it, kept so the UI can show why.</summary>
    public decimal? TriggeredRate { get; init; }

    public bool Triggered => TriggeredAt is not null;
}
