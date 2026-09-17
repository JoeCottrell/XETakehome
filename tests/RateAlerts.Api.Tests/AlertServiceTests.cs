using RateAlerts.Api.Alerts;
using RateAlerts.Api.Rates;
using Xunit;

namespace RateAlerts.Api.Tests;

public class AlertServiceTests
{
    private readonly InMemoryAlertStore _store = new();
    private readonly FakeRateProvider _rates = new();
    private readonly TestTimeProvider _clock = new(TestData.Now);
    private readonly AlertService _service;

    public AlertServiceTests()
    {
        _service = new AlertService(_store, _rates, _clock);
    }

    private static NewAlert NewAlert(string pair = "GBP/USD", decimal threshold = 1.30m, AlertDirection direction = AlertDirection.Above) =>
        new(CurrencyPair.Parse(pair), threshold, direction);

    [Fact]
    public async Task A_new_alert_is_evaluated_immediately()
    {
        _rates.Returns("GBP/USD", 1.3500m);

        var view = await _service.CreateAsync(NewAlert(threshold: 1.30m), default);

        Assert.True(view.Alert.Triggered);
        Assert.Equal(TestData.Now, view.Alert.TriggeredAt);
        Assert.Equal(1.3500m, view.Alert.TriggeredRate);
        Assert.Equal(1.3500m, view.Rate?.Mid);
    }

    [Fact]
    public async Task A_new_alert_below_its_threshold_waits()
    {
        _rates.Returns("GBP/USD", 1.2500m);

        var view = await _service.CreateAsync(NewAlert(threshold: 1.30m), default);

        Assert.False(view.Alert.Triggered);
        Assert.Equal(TestData.Now, view.Alert.CreatedAt);
    }

    [Fact]
    public async Task Listing_triggers_alerts_the_rate_has_since_passed_and_stores_the_result()
    {
        _rates.Returns("GBP/USD", 1.2500m);
        var created = await _service.CreateAsync(NewAlert(threshold: 1.30m), default);

        _clock.Advance(TimeSpan.FromMinutes(10));
        _rates.Returns("GBP/USD", 1.3100m);

        var listed = await _service.ListAsync(default);

        var view = Assert.Single(listed);
        Assert.True(view.Alert.Triggered);
        Assert.Equal(TestData.Now.AddMinutes(10), view.Alert.TriggeredAt);

        // The trigger is state, not a view concern: it survives into the store.
        var stored = await _store.FindAsync(created.Alert.Id, default);
        Assert.Equal(view.Alert, stored);
    }

    [Fact]
    public async Task A_triggered_alert_stays_triggered_after_the_rate_falls_back()
    {
        _rates.Returns("GBP/USD", 1.3500m);
        await _service.CreateAsync(NewAlert(threshold: 1.30m), default);

        _clock.Advance(TimeSpan.FromHours(1));
        _rates.Returns("GBP/USD", 1.1000m);

        var view = Assert.Single(await _service.ListAsync(default));

        Assert.True(view.Alert.Triggered);
        Assert.Equal(TestData.Now, view.Alert.TriggeredAt);
        Assert.Equal(1.3500m, view.Alert.TriggeredRate);
        Assert.Equal(1.1000m, view.Rate?.Mid);
    }

    [Fact]
    public async Task An_alert_with_no_available_rate_is_left_alone()
    {
        var created = await _service.CreateAsync(NewAlert(pair: "XXX/YYY", threshold: 1.30m), default);
        Assert.Null(created.Rate);

        var view = Assert.Single(await _service.ListAsync(default));

        Assert.False(view.Alert.Triggered);
        Assert.Null(view.Rate);
    }

    [Fact]
    public async Task Rates_are_requested_once_per_distinct_pair()
    {
        _rates.Returns("GBP/USD", 1.2500m).Returns("EUR/USD", 1.0500m);
        await _service.CreateAsync(NewAlert(pair: "GBP/USD", threshold: 1.90m), default);
        await _service.CreateAsync(NewAlert(pair: "GBP/USD", threshold: 1.95m), default);
        await _service.CreateAsync(NewAlert(pair: "EUR/USD", threshold: 1.90m), default);
        _rates.Requests.Clear();

        await _service.ListAsync(default);

        var requested = Assert.Single(_rates.Requests);
        Assert.Equal(2, requested.Count);
        Assert.Equal(["GBP/USD", "EUR/USD"], requested.Select(pair => pair.ToString()));
    }

    [Fact]
    public async Task Listing_with_no_alerts_does_not_call_the_rate_provider()
    {
        Assert.Empty(await _service.ListAsync(default));
        Assert.Equal(0, _rates.CallCount);
    }

    [Fact]
    public async Task Deleting_removes_the_alert_and_reports_unknown_ids()
    {
        _rates.Returns("GBP/USD", 1.2500m);
        var created = await _service.CreateAsync(NewAlert(), default);

        Assert.True(await _service.DeleteAsync(created.Alert.Id, default));
        Assert.False(await _service.DeleteAsync(created.Alert.Id, default));
        Assert.Empty(await _service.ListAsync(default));
    }

    [Fact]
    public async Task Find_returns_the_evaluated_alert_or_null()
    {
        _rates.Returns("GBP/USD", 1.2500m);
        var created = await _service.CreateAsync(NewAlert(threshold: 1.30m), default);

        _rates.Returns("GBP/USD", 1.3100m);
        var found = await _service.FindAsync(created.Alert.Id, default);

        Assert.NotNull(found);
        Assert.True(found.Alert.Triggered);
        Assert.Null(await _service.FindAsync(Guid.NewGuid(), default));
    }
}
