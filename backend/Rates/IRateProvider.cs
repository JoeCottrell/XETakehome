namespace RateAlerts.Api.Rates;

/// <summary>
/// Source of current rates. The interface exists so alert evaluation can be tested without the
/// network, and so caching can be layered on as a decorator.
/// </summary>
public interface IRateProvider
{
    /// <summary>
    /// Fetches the current rate for each requested pair.
    /// </summary>
    /// <returns>
    /// The rates that could be resolved. Pairs the provider could not price - unknown currencies,
    /// or an upstream failure - are simply absent, so one bad pair cannot fail the whole request.
    /// </returns>
    Task<IReadOnlyDictionary<CurrencyPair, Rate>> GetRatesAsync(
        IReadOnlyCollection<CurrencyPair> pairs,
        CancellationToken cancellationToken);
}
