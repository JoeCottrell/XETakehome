using Microsoft.AspNetCore.Mvc;
using RateAlerts.Api.Alerts;
using RateAlerts.Api.Rates;

namespace RateAlerts.Api.Controllers;

/// <summary>
/// Create, list and delete rate alerts. The controller only validates input and maps to the wire
/// shape; the rules live in <see cref="AlertService"/> and <see cref="AlertEvaluator"/>.
/// </summary>
[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly IAlertService _alerts;

    public AlertsController(IAlertService alerts)
    {
        _alerts = alerts;
    }

    /// <summary>Lists every alert, evaluated against current rates, oldest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> List(CancellationToken cancellationToken)
    {
        var views = await _alerts.ListAsync(cancellationToken);
        return Ok(views.Select(AlertResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var view = await _alerts.FindAsync(id, cancellationToken);
        return view is null ? NotFound() : Ok(AlertResponse.From(view));
    }

    [HttpPost]
    public async Task<ActionResult<AlertResponse>> Create(
        [FromBody] CreateAlertRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || !TryValidate(request, out var newAlert))
        {
            return ValidationProblem(ModelState);
        }

        var view = await _alerts.CreateAsync(newAlert, cancellationToken);
        var response = AlertResponse.From(view);

        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _alerts.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>
    /// Turns the request into a value the rest of the code can trust, collecting every problem so
    /// the caller sees all of them at once instead of one per round trip.
    /// </summary>
    private bool TryValidate(CreateAlertRequest request, out NewAlert newAlert)
    {
        newAlert = null!;

        if (!CurrencyPair.TryParse(request.Pair, out var pair))
        {
            ModelState.AddModelError(
                nameof(request.Pair),
                "Pair must be two different ISO currency codes in BASE/QUOTE form, for example GBP/USD.");
        }

        if (request.Threshold is not > 0)
        {
            ModelState.AddModelError(nameof(request.Threshold), "Threshold must be greater than zero.");
        }

        if (!AlertDirections.TryParse(request.Direction, out var direction))
        {
            ModelState.AddModelError(
                nameof(request.Direction),
                $"Direction must be '{AlertDirections.Above}' or '{AlertDirections.Below}'.");
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        newAlert = new NewAlert(pair, request.Threshold!.Value, direction);
        return true;
    }
}
