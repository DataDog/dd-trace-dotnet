using System;
using System.Threading;
using System.Threading.Tasks;
using Logger = Serilog.Log;

namespace SmokeTests;

public static class Helpers
{
    public static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15) };
    public static int MaxRetries => RetryDelays.Length;

    public static void LogSection(string title)
    {
        Logger.Information("────────────────────────────────────────────────────────────");
        Logger.Information("{Title}", title);
        Logger.Information("────────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// Retries an async action on failure. The number of retries equals <paramref name="retryDelays"/>.Length,
    /// so total attempts = retryDelays.Length + 1. Cancellation via <paramref name="ct"/> is never retried.
    /// </summary>
    public static async Task RetryAsync(string operation, Func<Task> action, TimeSpan[] retryDelays, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < retryDelays.Length && !ct.IsCancellationRequested)
            {
                Logger.Warning(ex, "{Operation} failed (attempt {Attempt}/{Total}), retrying in {Delay}s...",
                    operation, attempt + 1, retryDelays.Length + 1, retryDelays[attempt].TotalSeconds);
                await Task.Delay(retryDelays[attempt], ct);
            }
        }
    }

    /// <summary>
    /// Retries an async function on failure, returning its result on success. The number of retries equals <paramref name="retryDelays"/>.Length,
    /// so total attempts = retryDelays.Length + 1. Cancellation via <paramref name="ct"/> is never retried.
    /// </summary>
    public static async Task<T> RetryAsync<T>(string operation, Func<Task<T>> action, TimeSpan[] retryDelays, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < retryDelays.Length && !ct.IsCancellationRequested)
            {
                Logger.Warning(ex, "{Operation} failed (attempt {Attempt}/{Total}), retrying in {Delay}s...",
                    operation, attempt + 1, retryDelays.Length + 1, retryDelays[attempt].TotalSeconds);
                await Task.Delay(retryDelays[attempt], ct);
            }
        }
    }
    
}
