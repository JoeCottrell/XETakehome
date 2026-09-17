namespace RateAlerts.Api.Rates;

/// <summary>The wire shape of a board rate. Unchanged from the original response.</summary>
public sealed record RateResponse(string Pair, decimal Rate, DateTimeOffset AsOf)
{
    private const int DisplayDecimals = 4;

    public static RateResponse From(Rate rate) =>
        new(rate.Pair.ToString(), Math.Round(rate.Mid, DisplayDecimals), rate.AsOf);
}
