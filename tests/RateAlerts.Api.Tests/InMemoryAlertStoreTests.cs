using RateAlerts.Api.Alerts;
using Xunit;

namespace RateAlerts.Api.Tests;

public class InMemoryAlertStoreTests
{
    private readonly InMemoryAlertStore _store = new();

    [Fact]
    public async Task Lists_alerts_oldest_first()
    {
        var newest = TestData.Alert(createdAt: TestData.Now.AddMinutes(2));
        var oldest = TestData.Alert(createdAt: TestData.Now);

        await _store.AddAsync(newest, default);
        await _store.AddAsync(oldest, default);

        var listed = await _store.ListAsync(default);

        Assert.Equal([oldest.Id, newest.Id], listed.Select(alert => alert.Id));
    }

    [Fact]
    public async Task Finds_and_deletes_by_id()
    {
        var alert = TestData.Alert();
        await _store.AddAsync(alert, default);

        Assert.Equal(alert, await _store.FindAsync(alert.Id, default));
        Assert.True(await _store.DeleteAsync(alert.Id, default));
        Assert.Null(await _store.FindAsync(alert.Id, default));
        Assert.Empty(await _store.ListAsync(default));
    }

    [Fact]
    public async Task Deleting_an_unknown_id_reports_that_nothing_was_removed()
    {
        Assert.False(await _store.DeleteAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Replace_updates_the_stored_alert()
    {
        var alert = TestData.Alert();
        await _store.AddAsync(alert, default);
        var triggered = alert with { TriggeredAt = TestData.Now, TriggeredRate = 1.5m };

        Assert.True(await _store.TryReplaceAsync(alert, triggered, default));
        Assert.Equal(triggered, await _store.FindAsync(alert.Id, default));
    }

    [Fact]
    public async Task Replace_does_not_resurrect_a_deleted_alert()
    {
        var alert = TestData.Alert();
        await _store.AddAsync(alert, default);
        await _store.DeleteAsync(alert.Id, default);

        Assert.False(await _store.TryReplaceAsync(alert, alert with { TriggeredAt = TestData.Now }, default));
        Assert.Empty(await _store.ListAsync(default));
    }

    [Fact]
    public async Task Replace_loses_to_a_concurrent_update()
    {
        var alert = TestData.Alert();
        await _store.AddAsync(alert, default);
        var winner = alert with { TriggeredAt = TestData.Now, TriggeredRate = 1.5m };
        await _store.TryReplaceAsync(alert, winner, default);

        var loser = alert with { TriggeredAt = TestData.Now.AddMinutes(1), TriggeredRate = 1.6m };

        Assert.False(await _store.TryReplaceAsync(alert, loser, default));
        Assert.Equal(winner, await _store.FindAsync(alert.Id, default));
    }
}
