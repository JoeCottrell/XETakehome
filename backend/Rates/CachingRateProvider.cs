using Microsoft.Extensions.Options;

namespace RateAlerts.Api.Rates;

/// <summary>
/// Serves rates from <see cref="RateCache"/> when they are fresh enough, and only asks the inner
/// provider for the rest.
/// </summary>
/// <remarks>
/// Xe bills per request and the board polls on every page load, so a few seconds of staleness buys
/// a large reduction in upstream calls. The TTL is configuration, not a constant, and setting it to
/// zero disables caching entirely.
///
/// Two requests for the same cold pair can both reach the API; collapsing them behind a lock would
/// trade that rare duplicate for a shared failure path, and at this scale it is not worth it.
/// </remarks>
public sealed class CachingRateProvider : IRateProvider
{
    private readonly IRateProvider _inner;
    private readonly RateCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;

    public CachingRateProvider(
        IRateProvider inner,
        RateCache cache,
        TimeProvider timeProvider,
        IOptions<XecdOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _timeProvider = timeProvider;
        _timeToLive = TimeSpan.FromSeconds(options.Value.CacheSeconds);
    }

    public async Task<IReadOnlyDictionary<CurrencyPair, Rate>> GetRatesAsync(
        IReadOnlyCollection<CurrencyPair> pairs,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var rates = new Dictionary<CurrencyPair, Rate>();
        var missing = new List<CurrencyPair>();

        foreach (var pair in pairs.Distinct())
        {
            if (_cache.TryGet(pair, now, out var cached))
            {
                rates[pair] = cached;
            }
            else
            {
                missing.Add(pair);
            }
        }

        if (missing.Count == 0)
        {
            return rates;
        }

        var fetched = await _inner.GetRatesAsync(missing, cancellationToken);
        var expiresAt = _timeProvider.GetUtcNow() + _timeToLive;

        foreach (var rate in fetched.Values)
        {
            _cache.Set(rate, expiresAt);
            rates[rate.Pair] = rate;
        }

        return rates;
    }
}
