using Microsoft.Extensions.Options;
using RateAlerts.Api.Rates;
using Xunit;

namespace RateAlerts.Api.Tests;

public class CachingRateProviderTests
{
    private readonly FakeRateProvider _upstream = new();
    private readonly TestTimeProvider _clock = new(TestData.Now);
    private readonly RateCache _cache = new();

    private CachingRateProvider Provider(int cacheSeconds = 10) =>
        new(_upstream, _cache, _clock, Options.Create(new XecdOptions { CacheSeconds = cacheSeconds }));

    private static CurrencyPair[] Pairs(params string[] pairs) =>
        pairs.Select(CurrencyPair.Parse).ToArray();

    [Fact]
    public async Task Serves_a_second_request_from_the_cache()
    {
        _upstream.Returns("GBP/USD", 1.2500m);
        var provider = Provider();

        var first = await provider.GetRatesAsync(Pairs("GBP/USD"), default);
        _clock.Advance(TimeSpan.FromSeconds(9));
        var second = await provider.GetRatesAsync(Pairs("GBP/USD"), default);

        Assert.Equal(1, _upstream.CallCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Goes_back_upstream_once_the_entry_has_expired()
    {
        _upstream.Returns("GBP/USD", 1.2500m);
        var provider = Provider();

        await provider.GetRatesAsync(Pairs("GBP/USD"), default);
        _clock.Advance(TimeSpan.FromSeconds(11));
        _upstream.Returns("GBP/USD", 1.3000m);
        var refreshed = await provider.GetRatesAsync(Pairs("GBP/USD"), default);

        Assert.Equal(2, _upstream.CallCount);
        Assert.Equal(1.3000m, refreshed[CurrencyPair.Parse("GBP/USD")].Mid);
    }

    [Fact]
    public async Task Only_asks_upstream_for_the_pairs_it_is_missing()
    {
        _upstream.Returns("GBP/USD", 1.2500m).Returns("EUR/USD", 1.0500m);
        var provider = Provider();

        await provider.GetRatesAsync(Pairs("GBP/USD"), default);
        _upstream.Requests.Clear();

        var rates = await provider.GetRatesAsync(Pairs("GBP/USD", "EUR/USD"), default);

        var requested = Assert.Single(_upstream.Requests);
        Assert.Equal(["EUR/USD"], requested.Select(pair => pair.ToString()));
        Assert.Equal(2, rates.Count);
    }

    [Fact]
    public async Task A_pair_the_upstream_cannot_price_is_absent_and_is_not_cached()
    {
        var provider = Provider();

        var rates = await provider.GetRatesAsync(Pairs("XXX/YYY"), default);
        await provider.GetRatesAsync(Pairs("XXX/YYY"), default);

        Assert.Empty(rates);
        Assert.Equal(2, _upstream.CallCount);
    }

    [Fact]
    public async Task A_zero_second_ttl_disables_caching()
    {
        _upstream.Returns("GBP/USD", 1.2500m);
        var provider = Provider(cacheSeconds: 0);

        await provider.GetRatesAsync(Pairs("GBP/USD"), default);
        await provider.GetRatesAsync(Pairs("GBP/USD"), default);

        Assert.Equal(2, _upstream.CallCount);
    }

    [Fact]
    public async Task Duplicate_pairs_in_one_request_are_fetched_once()
    {
        _upstream.Returns("GBP/USD", 1.2500m);
        var provider = Provider();

        var rates = await provider.GetRatesAsync(Pairs("GBP/USD", "gbp/usd"), default);

        var requested = Assert.Single(_upstream.Requests);
        Assert.Single(requested);
        Assert.Single(rates);
    }
}
