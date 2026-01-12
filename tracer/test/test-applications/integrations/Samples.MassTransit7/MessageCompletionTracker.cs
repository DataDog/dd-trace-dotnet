using System.Collections.Concurrent;

namespace Samples.MassTransit7;

/// <summary>
/// Tracks message processing completion for deterministic test execution.
/// Allows the main program to wait until all expected messages have been consumed.
/// </summary>
public class MessageCompletionTracker
{
    private readonly ConcurrentDictionary<string, int> _expectedCounts = new();
    private readonly ConcurrentDictionary<string, int> _completedCounts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _completionSources = new();

    public void ExpectMessages(string messageType, int count)
    {
        _expectedCounts[messageType] = count;
        _completedCounts[messageType] = 0;
        _completionSources[messageType] = new TaskCompletionSource<bool>();
    }

    public void MessageCompleted(string messageType)
    {
        var completed = _completedCounts.AddOrUpdate(messageType, 1, (_, current) => current + 1);

        if (_expectedCounts.TryGetValue(messageType, out var expected) && completed >= expected)
        {
            if (_completionSources.TryGetValue(messageType, out var tcs))
            {
                tcs.TrySetResult(true);
            }
        }
    }

    public async Task WaitForCompletion(string messageType, TimeSpan? timeout = null)
    {
        if (!_completionSources.TryGetValue(messageType, out var tcs))
        {
            return; // No messages expected
        }

        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutValue));

        if (completedTask != tcs.Task)
        {
            var completed = _completedCounts.TryGetValue(messageType, out var c) ? c : 0;
            var expected = _expectedCounts.TryGetValue(messageType, out var e) ? e : 0;
            throw new TimeoutException($"Timeout waiting for {messageType} messages. Expected: {expected}, Completed: {completed}");
        }
    }

    public async Task WaitForAll(TimeSpan? timeout = null)
    {
        var tasks = _completionSources.Values.Select(tcs => tcs.Task).ToArray();
        if (tasks.Length == 0) return;

        var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeoutValue));

        if (completedTask != Task.WhenAll(tasks))
        {
            var status = _expectedCounts.Select(kvp =>
            {
                var completed = _completedCounts.TryGetValue(kvp.Key, out var c) ? c : 0;
                return $"{kvp.Key}: {completed}/{kvp.Value}";
            });
            throw new TimeoutException($"Timeout waiting for all messages. Status: {string.Join(", ", status)}");
        }
    }

    public void Reset()
    {
        _expectedCounts.Clear();
        _completedCounts.Clear();
        _completionSources.Clear();
    }
}
