// <copyright file="ConcurrentAdaptiveCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Caching
{
    internal enum CacheState
    {
        Valid,
        Error
    }

    /// <summary>
    /// Eviction policy for caching
    /// </summary>
    internal enum EvictionPolicy
    {
        /// <summary>
        /// Least Recently Used
        /// </summary>
        Lru,

        /// <summary>
        /// Least Frequently Used
        /// </summary>
        Lfu
    }

    internal sealed class ConcurrentAdaptiveCache<TKey, TValue> : IDisposable
        where TKey : notnull
    {
        private const int DefaultCapacity = 2048;
        private const int LowResourceCapacity = 512;
        private const int RestrictedEnvironmentCapacity = 1024;
        private const double CleanupIntervalAdjustmentFactor = 1.5;
        internal const int MinCleanupIntervalSeconds = 300;
        internal const int MaxCleanupIntervalSeconds = 3600; // 1 hour

        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<ConcurrentAdaptiveCache<TKey, TValue>>();

        private readonly TimeSpan _defaultSlidingExpiration = TimeSpan.FromMinutes(60);
        private readonly ITimeProvider _timeProvider;
        private readonly Dictionary<TKey, CacheItem<TValue>> _cache;
        private readonly ReaderWriterLockSlim _lock;
        private readonly int _capacity;
        private readonly IEvictionPolicy<TKey> _evictionPolicy;
        private readonly IEnvironmentChecker _environmentChecker;
        private readonly IMemoryChecker _memoryChecker;
        private readonly CancellationTokenSource _cleanupCancellationTokenSource;
        private readonly Task _cleanupTask;
        private volatile int _lastCleanupItemsRemoved;
        private long _hits;
        private long _misses;
        private int _disposed;
        private int _state;

        internal ConcurrentAdaptiveCache(
            int? capacity = null,
            IEvictionPolicy<TKey>? evictionPolicy = null,
            EvictionPolicy evictionPolicyKind = EvictionPolicy.Lru,
            ITimeProvider? timeProvider = null,
            IEqualityComparer<TKey>? comparer = null,
            IEnvironmentChecker? environmentChecker = null,
            IMemoryChecker? memoryChecker = null,
            int maxErrors = 3)
        {
            _evictionPolicy = evictionPolicy ?? CreateEvictionPolicy(evictionPolicyKind);
            _environmentChecker = environmentChecker ?? DefaultEnvironmentChecker.Instance;
            _memoryChecker = memoryChecker ?? DefaultMemoryChecker.Instance;
            _capacity = capacity ?? DetermineCapacity();
            Logger.Debug("ConcurrentAdaptiveCache capacity is: {Capacity}", (object)_capacity);
            _timeProvider = timeProvider ?? new DefaultTimeProvider();
            _cache = new Dictionary<TKey, CacheItem<TValue>>(_capacity, comparer);
            _lock = new ReaderWriterLockSlim();
            _cleanupCancellationTokenSource = new();
            CurrentCleanupInterval = _defaultSlidingExpiration;
            _lastCleanupItemsRemoved = 0;
            _hits = 0;
            _misses = 0;
            _lastCleanupItemsRemoved = -1;
            _cleanupTask = Task.Run(() => { _ = AdaptiveCleanupAsync(maxErrors); });
        }

        internal CacheState State
        {
            get
            {
                var state = Volatile.Read(ref _state);
                return state == 0 ? CacheState.Valid : CacheState.Error;
            }
        }

        internal TimeSpan CurrentCleanupInterval { get; private set; }

        internal int Count
        {
            get
            {
                ThrowIfInvalid();

                _lock.EnterReadLock();
                try
                {
                    return _cache.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        internal double HitRate
        {
            get
            {
                ThrowIfInvalid();

                long totalRequests = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
                if (totalRequests == 0)
                {
                    return 0;
                }

                return (double)Interlocked.Read(ref _hits) / totalRequests;
            }
        }

        private static bool IsExpired(CacheItem<TValue> item, DateTime now)
        {
            if (!item.SlidingExpiration.HasValue)
            {
                return false;
            }

            var expirationTime = item.LastAccessed + item.SlidingExpiration.Value;
            return now >= expirationTime;
        }

        internal void Add(TValue value, TimeSpan? slidingExpiration = null, params TKey[] keys)
        {
            ThrowIfInvalid();

            if (keys == null || keys.Length == 0)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterWriteLock();
            try
            {
                foreach (var key in keys)
                {
                    AddOrUpdate(key, value, slidingExpiration);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal void Add(IEnumerable<KeyValuePair<TKey, TValue>> items, TimeSpan? slidingExpiration = null)
        {
            ThrowIfInvalid();

            if (items == null)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                {
                    AddOrUpdate(item.Key, item.Value, slidingExpiration);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal bool TryGet(TKey key, out TValue? value)
        {
            ThrowIfInvalid();

            if (key is null)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterReadLock();

            try
            {
                if (_cache.TryGetValue(key, out var item))
                {
                    item.UpdateAccess(_timeProvider.UtcNow);
                    Interlocked.Increment(ref _hits);
                    value = item.Value;
                    _evictionPolicy.Access(key);
                    return true;
                }

                Interlocked.Increment(ref _misses);
                value = default;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        internal TValue? GetOrAdd(TKey key, Func<TKey, TValue?> valueFactory, TimeSpan? slidingExpiration = null)
        {
            ThrowIfInvalid();

            if (key is null)
            {
                throw NullKeyException.Instance;
            }

            if (TryGet(key, out TValue? value))
            {
                return value;
            }

            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out var item))
                {
                    return value;
                }

                value = valueFactory(key);
                AddOrUpdate(key, value, slidingExpiration);
                return value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private async Task AdaptiveCleanupAsync(int maxError)
        {
            try
            {
                int consecutiveErrors = 0;
                while (consecutiveErrors < maxError &&
                       !_cleanupCancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await _timeProvider.Delay(CurrentCleanupInterval, _cleanupCancellationTokenSource.Token).ConfigureAwait(false);
                        int itemsRemoved = PerformCleanup();
                        AdjustCleanupInterval(itemsRemoved);
                        consecutiveErrors = 0; // Reset on success
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        consecutiveErrors++;
                        if (consecutiveErrors < maxError)
                        {
                            Logger.Error(e, "Error during cleanup");
                        }
                        else
                        {
                            Interlocked.Exchange(ref _state, 1);
                            Logger.Error("Cache entered error state after {ConsecutiveErrors} consecutive cleanup failures", property: consecutiveErrors);

                            _lock.EnterWriteLock();
                            try
                            {
                                _cache.Clear();
                            }
                            finally
                            {
                                _lock.ExitWriteLock();
                            }

                            Dispose();
                            break;
                        }

                        throw;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
        }

        internal int PerformCleanup()
        {
            _lock.EnterWriteLock();
            try
            {
                var now = _timeProvider.UtcNow;
                var expiredItems = _cache.Where(kvp => IsExpired(kvp.Value, now)).ToList();

                foreach (var item in expiredItems)
                {
                    _cache.Remove(item.Key);
                }

                while (_cache.Count > _capacity)
                {
                    var keyToRemove = _evictionPolicy.Evict();
                    if (_cache.TryGetValue(keyToRemove, out var removedItem))
                    {
                        _cache.Remove(keyToRemove);
                        expiredItems.Add(new KeyValuePair<TKey, CacheItem<TValue>>(keyToRemove, removedItem));
                    }
                }

                return expiredItems.Count;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal void AdjustCleanupInterval(int itemsRemoved)
        {
            var lastRemoved = _lastCleanupItemsRemoved;
            if (lastRemoved == -1)
            {
                // First cleanup - set baseline
                _lastCleanupItemsRemoved = itemsRemoved;
                return;
            }

            if (itemsRemoved > lastRemoved)
            {
                // More items removed - decrease interval
                CurrentCleanupInterval = TimeSpan.FromSeconds(
                    Math.Max(
                        MinCleanupIntervalSeconds,
                        CurrentCleanupInterval.TotalSeconds / CleanupIntervalAdjustmentFactor));
            }
            else if (itemsRemoved < lastRemoved)
            {
                // Fewer items removed - increase interval
                CurrentCleanupInterval = TimeSpan.FromSeconds(
                    Math.Min(
                        MaxCleanupIntervalSeconds,
                        CurrentCleanupInterval.TotalSeconds * CleanupIntervalAdjustmentFactor));
            }

            _lastCleanupItemsRemoved = itemsRemoved;
        }

        private IEvictionPolicy<TKey> CreateEvictionPolicy(EvictionPolicy policy)
        {
            return policy switch
            {
                EvictionPolicy.Lru => new LruEvictionPolicy<TKey>(),
                EvictionPolicy.Lfu => new LfuEvictionPolicy<TKey>(),
                _ => throw new ArgumentException("Unsupported eviction policy", nameof(policy)),
            };
        }

        private int DetermineCapacity()
        {
            try
            {
                if (_environmentChecker.IsServerlessEnvironment || _memoryChecker.IsLowResourceEnvironment)
                {
                    return LowResourceCapacity;
                }

                return DefaultCapacity;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Fail to auto determine capacity");
                // If we encounter any exception during environment detection,
                // we assume we're in a restricted environment and use a middle-ground capacity
                return RestrictedEnvironmentCapacity;
            }
        }

        private void AddOrUpdate(TKey key, TValue? value, TimeSpan? slidingExpiration)
        {
            if (key is null)
            {
                throw NullKeyException.Instance;
            }

            if (_cache.ContainsKey(key))
            {
                UpdateItem(key, value);
                _evictionPolicy.Add(key);
            }
            else
            {
                if (_cache.Count >= _capacity)
                {
                    var keyToRemove = _evictionPolicy.Evict();
                    _cache.Remove(keyToRemove);
                }

                var actualSlidingExpiration = slidingExpiration ?? _defaultSlidingExpiration;
                var cacheItem = new CacheItem<TValue>(value, actualSlidingExpiration);
                _cache.Add(key, cacheItem);
                _evictionPolicy.Add(key);
            }
        }

        private void UpdateItem(TKey key, TValue? value)
        {
            if (key is null)
            {
                throw NullKeyException.Instance;
            }

            var item = _cache[key];
            item.Value = value;
            item.UpdateAccess(_timeProvider.UtcNow);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                try
                {
                    const int timeoutInSeconds = 2;
                    _cleanupCancellationTokenSource.Cancel();

                    if (!_cleanupTask.Wait(TimeSpan.FromSeconds(timeoutInSeconds)))
                    {
                        Logger.Error(
                            "Cleanup task during {Dispose} failed to complete within {Seconds} timeout. " +
                            "This could indicate a bug in the cleanup logic. " +
                            "The task will continue running in the background ",
                            property0: nameof(Dispose),
                            property1: timeoutInSeconds);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error occurred while disposing cache");
                }
                finally
                {
                    _cleanupCancellationTokenSource.Dispose();
                    _lock.Dispose();
                }
            }
        }

        private void ThrowIfInvalid()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 1) == 1)
            {
                throw InvalidCacheStateException.Instance;
            }

            if (Interlocked.CompareExchange(ref _disposed, 1, 1) == 1)
            {
                throw new ObjectDisposedException(nameof(ConcurrentAdaptiveCache<TKey, TValue>));
            }
        }
    }
}
