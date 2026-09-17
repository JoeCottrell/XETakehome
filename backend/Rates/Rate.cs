namespace RateAlerts.Api.Rates;

/// <summary>
/// A mid-market rate for a single pair, as reported upstream.
/// </summary>
/// <param name="Pair">The pair the rate is for.</param>
/// <param name="Mid">The mid-market rate, at the precision the provider gave us.</param>
/// <param name="AsOf">The provider's timestamp for the quote, not the time we fetched it.</param>
public sealed record Rate(CurrencyPair Pair, decimal Mid, DateTimeOffset AsOf);
