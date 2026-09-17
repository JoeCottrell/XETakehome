using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Controllers;

/// <summary>
/// The rate board: current rates for the configured pairs.
/// </summary>
[ApiController]
[Route("api/rates")]
public sealed class RatesController : ControllerBase
{
    private readonly IRateProvider _rateProvider;
    private readonly RateBoardOptions _board;
    private readonly ILogger<RatesController> _logger;

    public RatesController(
        IRateProvider rateProvider,
        IOptions<RateBoardOptions> board,
        ILogger<RatesController> logger)
    {
        _rateProvider = rateProvider;
        _board = board.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RateResponse>>> Get(CancellationToken cancellationToken)
    {
        var pairs = new List<CurrencyPair>(_board.Pairs.Length);

        foreach (var configured in _board.Pairs)
        {
            if (CurrencyPair.TryParse(configured, out var pair))
            {
                pairs.Add(pair);
            }
            else
            {
                _logger.LogWarning("Ignoring '{Pair}' in RateBoard:Pairs: not a BASE/QUOTE pair.", configured);
            }
        }

        var rates = await _rateProvider.GetRatesAsync(pairs, cancellationToken);

        if (pairs.Count > 0 && rates.Count == 0)
        {
            // Nothing at all came back: say so, rather than serving an empty board that looks like
            // "no pairs configured".
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Rates are unavailable",
                detail: "The upstream rate provider could not be reached. Try again shortly.");
        }

        // Ordered as configured, so the board does not reshuffle when a pair is slow.
        var response = pairs
            .Where(rates.ContainsKey)
            .Select(pair => RateResponse.From(rates[pair]))
            .ToList();

        return Ok(response);
    }
}
