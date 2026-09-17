using System.Collections.Concurrent;

namespace RateAlerts.Api.Rates;

/// <summary>
/// Holds recently fetched rates, keyed by pair. Registered as a singleton so the cache outlives the
/// request-scoped provider that reads it.
/// </summary>
public sealed class RateCache
{
    private readonly ConcurrentDictionary<CurrencyPair, Entry> _entries = new();

    public bool TryGet(CurrencyPair pair, DateTimeOffset now, out Rate rate)
    {
        if (_entries.TryGetValue(pair, out var entry) && now < entry.ExpiresAt)
        {
            rate = entry.Rate;
            return true;
        }

        rate = default!;
        return false;
    }

    public void Set(Rate rate, DateTimeOffset expiresAt) =>
        _entries[rate.Pair] = new Entry(rate, expiresAt);

    private readonly record struct Entry(Rate Rate, DateTimeOffset ExpiresAt);
}
