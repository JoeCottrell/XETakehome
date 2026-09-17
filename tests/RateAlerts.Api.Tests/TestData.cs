using RateAlerts.Api.Alerts;
using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Tests;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    public static Alert Alert(
        string pair = "GBP/USD",
        decimal threshold = 1.30m,
        AlertDirection direction = AlertDirection.Above,
        DateTimeOffset? createdAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Pair = CurrencyPair.Parse(pair),
            Threshold = threshold,
            Direction = direction,
            CreatedAt = createdAt ?? Now,
        };

    public static Rate Rate(string pair = "GBP/USD", decimal mid = 1.25m, DateTimeOffset? asOf = null) =>
        new(CurrencyPair.Parse(pair), mid, asOf ?? Now);
}
