using RateAlerts.Api.Alerts;
using Xunit;

namespace RateAlerts.Api.Tests;

public class AlertEvaluatorTests
{
    private static readonly DateTimeOffset ObservedAt = TestData.Now.AddMinutes(5);

    [Theory]
    [InlineData(AlertDirection.Above, 1.30, 1.3001, true)]
    [InlineData(AlertDirection.Above, 1.30, 1.30, false)]
    [InlineData(AlertDirection.Above, 1.30, 1.2999, false)]
    [InlineData(AlertDirection.Below, 1.30, 1.2999, true)]
    [InlineData(AlertDirection.Below, 1.30, 1.30, false)]
    [InlineData(AlertDirection.Below, 1.30, 1.3001, false)]
    public void Fires_only_strictly_past_the_threshold(
        AlertDirection direction,
        decimal threshold,
        decimal rate,
        bool expectedToFire)
    {
        var alert = TestData.Alert(threshold: threshold, direction: direction);

        var evaluated = AlertEvaluator.Evaluate(alert, TestData.Rate(mid: rate), ObservedAt);

        Assert.Equal(expectedToFire, evaluated.Triggered);
    }

    [Fact]
    public void Records_when_and_at_what_rate_it_fired()
    {
        var alert = TestData.Alert(threshold: 1.30m, direction: AlertDirection.Above);

        var evaluated = AlertEvaluator.Evaluate(alert, TestData.Rate(mid: 1.3456m), ObservedAt);

        Assert.True(evaluated.Triggered);
        Assert.Equal(ObservedAt, evaluated.TriggeredAt);
        Assert.Equal(1.3456m, evaluated.TriggeredRate);
    }

    [Fact]
    public void Evaluating_does_not_mutate_the_alert_it_was_given()
    {
        var alert = TestData.Alert(threshold: 1.30m, direction: AlertDirection.Above);

        AlertEvaluator.Evaluate(alert, TestData.Rate(mid: 1.40m), ObservedAt);

        Assert.False(alert.Triggered);
    }

    [Fact]
    public void Once_fired_an_alert_stays_fired_when_the_rate_moves_back()
    {
        var alert = TestData.Alert(threshold: 1.30m, direction: AlertDirection.Above);

        var triggered = AlertEvaluator.Evaluate(alert, TestData.Rate(mid: 1.35m), ObservedAt);
        var later = AlertEvaluator.Evaluate(triggered, TestData.Rate(mid: 1.20m), ObservedAt.AddHours(1));

        Assert.True(later.Triggered);
        Assert.Equal(ObservedAt, later.TriggeredAt);
        Assert.Equal(1.35m, later.TriggeredRate);
        Assert.Same(triggered, later);
    }

    [Fact]
    public void An_untriggered_alert_is_returned_unchanged()
    {
        var alert = TestData.Alert(threshold: 1.30m, direction: AlertDirection.Above);

        var evaluated = AlertEvaluator.Evaluate(alert, TestData.Rate(mid: 1.20m), ObservedAt);

        Assert.Same(alert, evaluated);
    }

    [Fact]
    public void Refuses_a_rate_for_a_different_pair()
    {
        var alert = TestData.Alert(pair: "GBP/USD");

        Assert.Throws<ArgumentException>(() =>
            AlertEvaluator.Evaluate(alert, TestData.Rate(pair: "EUR/USD", mid: 99m), ObservedAt));
    }
}
