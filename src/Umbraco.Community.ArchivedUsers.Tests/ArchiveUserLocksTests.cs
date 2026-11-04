using Umbraco.Community.ArchivedUsers.Core.Services;

namespace Umbraco.Community.ArchivedUsers.Tests;

public class ArchiveUserLocksTests
{
    [Fact]
    public async Task Same_key_waits_until_the_first_handle_is_disposed()
    {
        var locks = new ArchiveUserLocks();
        var key = Guid.NewGuid();

        IDisposable first = await locks.AcquireAsync(key);
        Task<IDisposable> second = locks.AcquireAsync(key);
        await Task.Delay(50);
        Assert.False(second.IsCompleted);

        first.Dispose();
        using IDisposable acquired = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(second.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Cancellation_while_waiting_throws_OperationCanceledException()
    {
        var locks = new ArchiveUserLocks();
        var key = Guid.NewGuid();
        using IDisposable held = await locks.AcquireAsync(key);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => locks.AcquireAsync(key, cts.Token));
    }

    [Fact]
    public async Task Disposing_twice_releases_once()
    {
        var locks = new ArchiveUserLocks();
        var key = Guid.NewGuid();
        IDisposable first = await locks.AcquireAsync(key);
        first.Dispose();
        first.Dispose();

        using IDisposable second = await locks.AcquireAsync(key);
        Task<IDisposable> third = locks.AcquireAsync(key);
        await Task.Delay(50);

        Assert.False(third.IsCompleted, "a double dispose must not release the stripe twice");
    }
}
