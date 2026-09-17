namespace RateAlerts.Api.Tests;

/// <summary>
/// A clock the tests drive by hand. Small enough not to be worth a package dependency, and it keeps
/// the time-sensitive assertions (trigger stamps, cache expiry) exact rather than approximate.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
