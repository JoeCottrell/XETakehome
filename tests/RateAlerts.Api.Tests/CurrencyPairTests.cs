using RateAlerts.Api.Rates;
using Xunit;

namespace RateAlerts.Api.Tests;

public class CurrencyPairTests
{
    [Theory]
    [InlineData("GBP/USD", "GBP", "USD")]
    [InlineData("gbp/usd", "GBP", "USD")]
    [InlineData(" GBP / USD ", "GBP", "USD")]
    public void Parses_valid_pairs_and_normalises_case(string input, string expectedBase, string expectedQuote)
    {
        Assert.True(CurrencyPair.TryParse(input, out var pair));
        Assert.Equal(expectedBase, pair.Base);
        Assert.Equal(expectedQuote, pair.Quote);
        Assert.Equal($"{expectedBase}/{expectedQuote}", pair.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GBP")]
    [InlineData("GBP/")]
    [InlineData("GBP/US")]
    [InlineData("GBPP/USD")]
    [InlineData("GB1/USD")]
    [InlineData("GBP/USD/EUR")]
    [InlineData("USD/USD")]
    public void Rejects_anything_that_is_not_two_different_currency_codes(string? input)
    {
        Assert.False(CurrencyPair.TryParse(input, out _));
    }

    [Fact]
    public void Pairs_that_differ_only_by_case_are_the_same_pair()
    {
        Assert.Equal(CurrencyPair.Parse("gbp/usd"), CurrencyPair.Parse("GBP/USD"));
        Assert.NotEqual(CurrencyPair.Parse("GBP/USD"), CurrencyPair.Parse("USD/GBP"));
    }

    [Fact]
    public void Parse_throws_on_malformed_input()
    {
        Assert.Throws<FormatException>(() => CurrencyPair.Parse("not a pair"));
    }
}
