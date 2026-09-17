using System.Text.Json;

namespace RateAlerts.Api.Rates;

/// <summary>
/// Fetches live rates from the Xe Currency Data API.
/// </summary>
/// <remarks>
/// Registered as a typed <see cref="HttpClient"/>, so the client (and its connection pool and auth
/// header) is managed by <c>IHttpClientFactory</c> rather than newed up per request as it was
/// before. Requests are grouped by base currency, because convert_from prices many quote currencies
/// in a single call, and the groups are issued concurrently.
/// </remarks>
public sealed class XeRateProvider : IRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XeRateProvider> _logger;

    public XeRateProvider(HttpClient httpClient, ILogger<XeRateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<CurrencyPair, Rate>> GetRatesAsync(
        IReadOnlyCollection<CurrencyPair> pairs,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return new Dictionary<CurrencyPair, Rate>();
        }

        var groups = pairs
            .Distinct()
            .GroupBy(pair => pair.Base)
            .ToList();

        var results = await Task.WhenAll(groups.Select(group =>
            FetchGroupAsync(group.Key, group.Select(pair => pair.Quote).ToArray(), cancellationToken)));

        return results
            .SelectMany(rates => rates)
            .ToDictionary(rate => rate.Pair, rate => rate);
    }

    private async Task<IReadOnlyList<Rate>> FetchGroupAsync(
        string baseCurrency,
        IReadOnlyList<string> quoteCurrencies,
        CancellationToken cancellationToken)
    {
        var requestUri = $"convert_from.json/?from={baseCurrency}&to={string.Join(',', quoteCurrencies)}&amount=1";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Xe returned {StatusCode} for {BaseCurrency} against {QuoteCurrencies}.",
                    (int)response.StatusCode,
                    baseCurrency,
                    string.Join(',', quoteCurrencies));
                return [];
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            return ReadRates(baseCurrency, document.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
                                          || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // One unreachable or malformed group should not take down the whole response: the caller
            // treats a missing pair as "no rate right now" and keeps serving the rest.
            _logger.LogWarning(
                exception,
                "Could not fetch rates for {BaseCurrency} against {QuoteCurrencies}.",
                baseCurrency,
                string.Join(',', quoteCurrencies));
            return [];
        }
    }

    /// <summary>
    /// Reads a convert_from response. Anything we cannot make sense of is skipped rather than
    /// throwing, so a single odd entry does not lose the rest of the payload.
    /// </summary>
    internal static IReadOnlyList<Rate> ReadRates(string baseCurrency, JsonElement root)
    {
        var asOf = root.TryGetProperty("timestamp", out var timestamp)
                   && timestamp.ValueKind == JsonValueKind.String
                   && DateTimeOffset.TryParse(timestamp.GetString(), out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        if (!root.TryGetProperty("to", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rates = new List<Rate>(quotes.GetArrayLength());

        foreach (var quote in quotes.EnumerateArray())
        {
            if (!quote.TryGetProperty("quotecurrency", out var quoteCurrency)
                || quoteCurrency.ValueKind != JsonValueKind.String
                || !quote.TryGetProperty("mid", out var mid)
                || mid.ValueKind != JsonValueKind.Number
                || !mid.TryGetDecimal(out var value)
                || !CurrencyPair.TryParse($"{baseCurrency}/{quoteCurrency.GetString()}", out var pair))
            {
                continue;
            }

            rates.Add(new Rate(pair, value, asOf));
        }

        return rates;
    }
}
