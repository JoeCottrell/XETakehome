using System.Text.Json;
using RateAlerts.Api.Rates;
using Xunit;

namespace RateAlerts.Api.Tests;

/// <summary>
/// Covers the parsing of a convert_from response - the part of the provider that breaks silently if
/// the payload is not what we assumed.
/// </summary>
public class XeRateProviderTests
{
    private static IReadOnlyList<Rate> Read(string baseCurrency, string json) =>
        XeRateProvider.ReadRates(baseCurrency, JsonDocument.Parse(json).RootElement);

    [Fact]
    public void Reads_every_quote_currency_in_the_response()
    {
        var rates = Read("USD", """
        {
          "from": "USD",
          "amount": 1.0,
          "timestamp": "2026-01-15T09:30:00Z",
          "to": [
            { "quotecurrency": "CAD", "mid": 1.3650123 },
            { "quotecurrency": "EUR", "mid": 0.92 }
          ]
        }
        """);

        Assert.Equal(2, rates.Count);
        Assert.Equal("USD/CAD", rates[0].Pair.ToString());
        Assert.Equal(1.3650123m, rates[0].Mid);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero), rates[0].AsOf);
        Assert.Equal("USD/EUR", rates[1].Pair.ToString());
    }

    [Fact]
    public void Skips_entries_it_cannot_make_sense_of_and_keeps_the_rest()
    {
        var rates = Read("USD", """
        {
          "timestamp": "2026-01-15T09:30:00Z",
          "to": [
            { "quotecurrency": "CAD" },
            { "mid": 1.1 },
            { "quotecurrency": "EURO", "mid": 0.92 },
            { "quotecurrency": "USD", "mid": 1.0 },
            { "quotecurrency": "GBP", "mid": "0.79" },
            { "quotecurrency": "JPY", "mid": 155.25 }
          ]
        }
        """);

        var rate = Assert.Single(rates);
        Assert.Equal("USD/JPY", rate.Pair.ToString());
        Assert.Equal(155.25m, rate.Mid);
    }

    [Fact]
    public void Falls_back_to_now_when_the_timestamp_is_missing_or_unparseable()
    {
        var before = DateTimeOffset.UtcNow;

        var rate = Assert.Single(Read("USD", """
        { "timestamp": "not a date", "to": [ { "quotecurrency": "CAD", "mid": 1.36 } ] }
        """));

        Assert.InRange(rate.AsOf, before, DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("""{ "timestamp": "2026-01-15T09:30:00Z" }""")]
    [InlineData("""{ "to": {} }""")]
    [InlineData("""{ "to": [] }""")]
    public void Returns_nothing_when_the_payload_has_no_quotes(string json)
    {
        Assert.Empty(Read("USD", json));
    }
}
