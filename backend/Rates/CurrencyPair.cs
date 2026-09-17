namespace RateAlerts.Api.Rates;

/// <summary>
/// A currency pair such as GBP/USD, held as two ISO 4217 alphabetic codes.
/// </summary>
/// <remarks>
/// Parsing lives here rather than in the controller so that everything downstream of validation
/// works with a value that cannot be malformed. Codes are upper-cased on the way in, so "gbp/usd"
/// and "GBP/USD" are the same pair and compare equal.
/// </remarks>
public readonly record struct CurrencyPair
{
    private CurrencyPair(string baseCurrency, string quoteCurrency)
    {
        Base = baseCurrency;
        Quote = quoteCurrency;
    }

    /// <summary>The currency being priced, e.g. GBP in GBP/USD.</summary>
    public string Base { get; }

    /// <summary>The currency it is priced in, e.g. USD in GBP/USD.</summary>
    public string Quote { get; }

    public static CurrencyPair Parse(string value) =>
        TryParse(value, out var pair)
            ? pair
            : throw new FormatException($"'{value}' is not a currency pair in BASE/QUOTE form.");

    /// <summary>
    /// Parses "BASE/QUOTE". We only check the shape (three letters either side, and not the same
    /// currency twice); whether the pair is actually quoted by the upstream API is its business.
    /// </summary>
    public static bool TryParse(string? value, out CurrencyPair pair)
    {
        pair = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        var baseCurrency = parts[0].Trim().ToUpperInvariant();
        var quoteCurrency = parts[1].Trim().ToUpperInvariant();

        if (!IsCurrencyCode(baseCurrency) || !IsCurrencyCode(quoteCurrency) || baseCurrency == quoteCurrency)
        {
            return false;
        }

        pair = new CurrencyPair(baseCurrency, quoteCurrency);
        return true;
    }

    private static bool IsCurrencyCode(string code) =>
        code.Length == 3 && code.All(char.IsAsciiLetter);

    public override string ToString() => $"{Base}/{Quote}";
}
