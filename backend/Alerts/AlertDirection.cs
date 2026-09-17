namespace RateAlerts.Api.Alerts;

/// <summary>Which side of the threshold the user is watching for.</summary>
public enum AlertDirection
{
    /// <summary>Fires when the rate rises strictly above the threshold.</summary>
    Above,

    /// <summary>Fires when the rate falls strictly below the threshold.</summary>
    Below,
}

/// <summary>
/// Translation between <see cref="AlertDirection"/> and the "above"/"below" strings used on the
/// wire. Kept out of the controller so the accepted values are defined - and tested - in one place.
/// </summary>
public static class AlertDirections
{
    public const string Above = "above";
    public const string Below = "below";

    /// <summary>
    /// Parses a wire value. Case-insensitive and tolerant of surrounding whitespace, but it accepts
    /// only the two documented words - not, say, "1" or "Up".
    /// </summary>
    public static bool TryParse(string? value, out AlertDirection direction)
    {
        var trimmed = value?.Trim();

        if (string.Equals(trimmed, Above, StringComparison.OrdinalIgnoreCase))
        {
            direction = AlertDirection.Above;
            return true;
        }

        if (string.Equals(trimmed, Below, StringComparison.OrdinalIgnoreCase))
        {
            direction = AlertDirection.Below;
            return true;
        }

        direction = default;
        return false;
    }

    public static string ToWireValue(this AlertDirection direction) => direction switch
    {
        AlertDirection.Above => Above,
        AlertDirection.Below => Below,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown alert direction."),
    };
}
