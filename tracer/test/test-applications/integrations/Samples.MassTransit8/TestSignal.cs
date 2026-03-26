using System.Collections.Concurrent;

namespace Samples.MassTransit8;

internal static class TestSignal
{
    static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> Signals = new();

    public static async Task WaitAsync(string key, TimeSpan timeout)
    {
        var signal = Signals.GetOrAdd(key, CreateSignal);
        var completed = await Task.WhenAny(signal.Task, Task.Delay(timeout)).ConfigureAwait(false);
        if (completed != signal.Task)
        {
            throw new TimeoutException($"Timed out waiting for signal '{key}'.");
        }

        Signals.TryRemove(key, out _);
    }

    public static void Set(string key)
    {
        Signals.GetOrAdd(key, CreateSignal).TrySetResult(true);
    }

    static TaskCompletionSource<bool> CreateSignal(string _) => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
