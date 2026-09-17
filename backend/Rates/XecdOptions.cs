using System.ComponentModel.DataAnnotations;

namespace RateAlerts.Api.Rates;

/// <summary>
/// Settings for the Xe Currency Data API. Bound from the "Xecd" configuration section and validated
/// at start-up, so a missing key is a loud failure on boot rather than a 500 on the first request.
/// </summary>
public sealed class XecdOptions
{
    public const string SectionName = "Xecd";

    [Required(AllowEmptyStrings = false)]
    public string AccountId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = "https://xecdapi.xe.com/v1/";

    /// <summary>How long a fetched rate stays usable before we go back to the API.</summary>
    [Range(0, 3600)]
    public int CacheSeconds { get; set; } = 10;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;
}
