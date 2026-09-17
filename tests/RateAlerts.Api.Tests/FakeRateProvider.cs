using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Tests;

/// <summary>
/// A rate provider the tests control. Records what it was asked for, so tests can assert that the
/// caching layer only requests what it is missing.
/// </summary>
internal sealed class FakeRateProvider : IRateProvider
{
    private readonly Dictionary<CurrencyPair, Rate> _rates = new();

    public List<IReadOnlyCollection<CurrencyPair>> Requests { get; } = [];

    public int CallCount => Requests.Count;

    public FakeRateProvider Returns(string pair, decimal mid, DateTimeOffset? asOf = null)
    {
        var rate = TestData.Rate(pair, mid, asOf);
        _rates[rate.Pair] = rate;
        return this;
    }

    public void Forget(string pair) => _rates.Remove(CurrencyPair.Parse(pair));

    public Task<IReadOnlyDictionary<CurrencyPair, Rate>> GetRatesAsync(
        IReadOnlyCollection<CurrencyPair> pairs,
        CancellationToken cancellationToken)
    {
        Requests.Add(pairs.ToList());

        IReadOnlyDictionary<CurrencyPair, Rate> result = pairs
            .Where(_rates.ContainsKey)
            .ToDictionary(pair => pair, pair => _rates[pair]);

        return Task.FromResult(result);
    }
}
