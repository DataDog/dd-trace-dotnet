// <copyright file="SharedSamplerScheduler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.RateLimiting
{
    /// <summary>
    /// Thread-safe shared scheduler that uses a single timer for multiple sampler callbacks.
    /// Groups callbacks by interval to minimize timer overhead.
    /// Properly handles disposal to prevent use-after-dispose scenarios.
    /// </summary>
    internal class SharedSamplerScheduler : ISamplerScheduler, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SharedSamplerScheduler>();

        private readonly ConcurrentDictionary<TimeSpan, IntervalGroup> _groups = new();
        private readonly object _disposeLock = new object();
        private volatile bool _disposed;

        public IDisposable Schedule(Action callback, TimeSpan interval)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (interval <= TimeSpan.Zero)
            {
                throw new ArgumentException("Interval must be positive", nameof(interval));
            }

            // Lock to prevent race with Dispose()
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SharedSamplerScheduler));
                }

                var group = _groups.GetOrAdd(interval, i => new IntervalGroup(i));
                var subscription = new Subscription(callback, group);
                group.Add(subscription);

                return subscription;
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                // Dispose all groups
                foreach (var group in _groups.Values)
                {
                    try
                    {
                        group.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "SharedSamplerScheduler: Error disposing interval group");
                    }
                }

                _groups.Clear();
            }
        }

        private class IntervalGroup : IDisposable
        {
            private readonly TimeSpan _interval;
            private readonly List<Subscription> _subscriptions = new();
            private readonly object _lock = new object();
            private readonly Timer _timer;
            private volatile bool _disposed;

            public IntervalGroup(TimeSpan interval)
            {
                _interval = interval;
                _timer = new Timer(OnTick, null, interval, interval);
            }

            public void Add(Subscription subscription)
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(IntervalGroup));
                    }

                    _subscriptions.Add(subscription);
                }
            }

            public void Remove(Subscription subscription)
            {
                lock (_lock)
                {
                    if (!_disposed)
                    {
                        _subscriptions.Remove(subscription);
                    }
                }
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                }

                // Dispose timer outside lock to prevent deadlock
                // Timer.Dispose() waits for executing callbacks, which try to acquire _lock
                _timer?.Dispose();
            }

            private void OnTick(object? state)
            {
                // Fast check without lock
                if (_disposed)
                {
                    return;
                }

                List<Subscription>? subscriptions = null;

                // Take a snapshot of subscriptions under lock
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (_subscriptions.Count == 0)
                    {
                        return;
                    }

                    // Copy to array to avoid allocation - more efficient than List<T> constructor
                    subscriptions = new List<Subscription>(_subscriptions.Count);
                    foreach (var sub in _subscriptions)
                    {
                        subscriptions.Add(sub);
                    }
                }

                // Execute callbacks outside lock
                if (subscriptions != null)
                {
                    foreach (var subscription in subscriptions)
                    {
                        try
                        {
                            subscription.Callback();
                        }
                        catch (Exception ex)
                        {
                            // Never let callback exception crash the timer
                            Log.Error(ex, "SharedSamplerScheduler: Error invoking callback");
                        }
                    }
                }
            }
        }

        private class Subscription : IDisposable
        {
            private readonly IntervalGroup _group;
            private int _disposed;

            public Subscription(Action callback, IntervalGroup group)
            {
                Callback = callback ?? throw new ArgumentNullException(nameof(callback));
                _group = group ?? throw new ArgumentNullException(nameof(group));
            }

            public Action Callback { get; }

            public void Dispose()
            {
                // Ensure we only remove once
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    try
                    {
                        _group.Remove(this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "SharedSamplerScheduler: Error removing subscription");
                    }
                }
            }
        }
    }
}
