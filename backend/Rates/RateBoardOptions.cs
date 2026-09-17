using System.ComponentModel.DataAnnotations;

namespace RateAlerts.Api.Rates;

/// <summary>
/// The pairs shown on the rate board. These were hard-coded in the controller; they are the kind of
/// thing that changes without a code change, so they live in configuration.
/// </summary>
public sealed class RateBoardOptions
{
    public const string SectionName = "RateBoard";

    [MinLength(1)]
    public string[] Pairs { get; set; } = [];
}
