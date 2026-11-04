using System.Collections.Concurrent;

namespace Umbraco.Community.ArchivedUsers.Web.Filters;

/// <summary>
/// Rate-limits list-filter log entries to one per hour per key. A DI singleton, so the scoped filter keeps no
/// static state (P2).
/// </summary>
public sealed class ArchivedUserListWarningClock
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _last = new();

    public bool ShouldWarn(string key, DateTimeOffset now)
    {
        while (true)
        {
            if (!_last.TryGetValue(key, out DateTimeOffset previous))
            {
                if (_last.TryAdd(key, now))
                {
                    return true;
                }

                continue;
            }

            if (now - previous < Interval)
            {
                return false;
            }

            if (_last.TryUpdate(key, now, previous))
            {
                return true;
            }
        }
    }
}
