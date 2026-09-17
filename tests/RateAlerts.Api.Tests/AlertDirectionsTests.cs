using RateAlerts.Api.Alerts;
using Xunit;

namespace RateAlerts.Api.Tests;

public class AlertDirectionsTests
{
    [Theory]
    [InlineData("above", AlertDirection.Above)]
    [InlineData("ABOVE", AlertDirection.Above)]
    [InlineData(" below ", AlertDirection.Below)]
    public void Parses_the_documented_words_case_insensitively(string input, AlertDirection expected)
    {
        Assert.True(AlertDirections.TryParse(input, out var direction));
        Assert.Equal(expected, direction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("up")]
    [InlineData("0")]
    [InlineData("Above threshold")]
    public void Rejects_everything_else(string? input)
    {
        Assert.False(AlertDirections.TryParse(input, out _));
    }

    [Theory]
    [InlineData(AlertDirection.Above, "above")]
    [InlineData(AlertDirection.Below, "below")]
    public void Wire_values_round_trip(AlertDirection direction, string expected)
    {
        Assert.Equal(expected, direction.ToWireValue());
        Assert.True(AlertDirections.TryParse(direction.ToWireValue(), out var parsed));
        Assert.Equal(direction, parsed);
    }
}
