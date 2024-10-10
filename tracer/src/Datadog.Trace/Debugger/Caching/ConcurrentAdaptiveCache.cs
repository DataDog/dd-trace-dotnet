// <copyright file="ConcurrentAdaptiveCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Caching
{
    /// <summary>
    /// Eviction policy for caching
    /// </summary>
    public enum EvictionPolicy
    {
        /// <summary>
        /// Least Recently Used
        /// </summary>
        LRU,

        /// <summary>
        /// Least Frequently Used
        /// </summary>
        LFU
    }

    internal class ConcurrentAdaptiveCache<TKey, TValue> : IDisposable
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
        private readonly Dictionary<TKey, CacheItem<TValue>> _cache;
        private readonly ReaderWriterLockSlim _lock;
        private readonly int _capacity;
        private readonly IEvictionPolicy<TKey> _evictionPolicy;
        private readonly IEnvironmentChecker _environmentChecker;
        private readonly IMemoryChecker _memoryChecker;
        private readonly CancellationTokenSource _cleanupCancellationTokenSource = new();
        private readonly Task _cleanupTask;
        private int _lastCleanupItemsRemoved;
        private long _hits;
        private long _misses;
        private bool _disposed;

        internal ConcurrentAdaptiveCache(
            int? capacity = null,
            IEvictionPolicy<TKey> evictionPolicy = null,
            EvictionPolicy evictionPolicyKind = EvictionPolicy.LRU,
            IEqualityComparer<TKey> comparer = null,
            IEnvironmentChecker environmentChecker = null,
            IMemoryChecker memoryChecker = null)
        {
            _evictionPolicy = evictionPolicy ?? CreateEvictionPolicy(evictionPolicyKind);
            _environmentChecker = environmentChecker ?? DefaultEnvironmentChecker.Instance;
            _memoryChecker = memoryChecker ?? DefaultMemoryChecker.Instance;
            _capacity = capacity ?? DetermineCapacity();
            Logger.Information("Cache capacity is: {Capacity}", (object)_capacity);
            _cache = new Dictionary<TKey, CacheItem<TValue>>(_capacity, comparer);
            _lock = new ReaderWriterLockSlim();
            _hits = 0;
            _misses = 0;
            CurrentCleanupInterval = _defaultSlidingExpiration;
            _lastCleanupItemsRemoved = 0;
            _cleanupTask = Task.Run(AdaptiveCleanupAsync);
        }

        internal TimeSpan CurrentCleanupInterval { get; private set; }

        internal int Count
        {
            get
            {
                ThrowIfDisposed();

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
                ThrowIfDisposed();

                long totalRequests = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
                if (totalRequests == 0)
                {
                    return 0;
                }

                return (double)Interlocked.Read(ref _hits) / totalRequests;
            }
        }

        internal void Add(TValue value, TimeSpan? slidingExpiration = null, params TKey[] keys)
        {
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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

        internal bool TryGet(TKey key, out TValue value)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterReadLock();

            try
            {
                if (_cache.TryGetValue(key, out CacheItem<TValue> item))
                {
                    item.LastAccessed = DateTime.UtcNow;
                    Interlocked.Increment(ref _hits);
                    item.IncrementAccessCount();
                    value = item.Value;
                    _evictionPolicy.Access(key);
                    return true;
                }

                Interlocked.Increment(ref _misses);
                value = default(TValue);
                return false;
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                {
                    _lock.ExitReadLock();
                }
            }
        }

        internal TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan? slidingExpiration = null)
        {
            ThrowIfDisposed();

            if (key == null)
            {
                throw NullKeyException.Instance;
            }

            if (TryGet(key, out TValue value))
            {
                return value;
            }

            _lock.EnterWriteLock();
            try
            {
                value = valueFactory(key);
                AddOrUpdate(key, value, slidingExpiration);
                return value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal async Task AdaptiveCleanupAsync()
        {
            while (!_cleanupCancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(CurrentCleanupInterval, _cleanupCancellationTokenSource.Token).ConfigureAwait(false);

                try
                {
                    int itemsRemoved = PerformCleanup();
                    AdjustCleanupInterval(itemsRemoved);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, nameof(AdaptiveCleanupAsync));
                }
            }
        }

        internal int PerformCleanup()
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var expiredItems = _cache.Where(kvp => IsExpired(kvp.Value, now)).ToList();

                foreach (var item in expiredItems)
                {
                    _cache.Remove(item.Key);
                    _evictionPolicy.Remove(item.Key);
                }

                while (_cache.Count > _capacity)
                {
                    var keyToRemove = _evictionPolicy.Evict();
                    _cache.Remove(keyToRemove);
                    expiredItems.Add(new KeyValuePair<TKey, CacheItem<TValue>>(keyToRemove, null));
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
            if (itemsRemoved > _lastCleanupItemsRemoved)
            {
                // More items were removed this time, so we should clean up more frequently
                CurrentCleanupInterval = TimeSpan.FromSeconds(Math.Max(MinCleanupIntervalSeconds, CurrentCleanupInterval.TotalSeconds / CleanupIntervalAdjustmentFactor));
            }
            else if (itemsRemoved < _lastCleanupItemsRemoved)
            {
                // Fewer items were removed this time, so we can clean up less frequently
                CurrentCleanupInterval = TimeSpan.FromSeconds(Math.Min(MaxCleanupIntervalSeconds, CurrentCleanupInterval.TotalSeconds * CleanupIntervalAdjustmentFactor));
            }

            _lastCleanupItemsRemoved = itemsRemoved;
        }

        private IEvictionPolicy<TKey> CreateEvictionPolicy(EvictionPolicy policy)
        {
            return policy switch
            {
                EvictionPolicy.LRU => new LRUEvictionPolicy<TKey>(),
                EvictionPolicy.LFU => new LFUEvictionPolicy<TKey>(),
                _ => throw new ArgumentException("Unsupported eviction policy", nameof(policy)),
            };
        }

        private int DetermineCapacity()
        {
            try
            {
                if (_environmentChecker.IsServerlessEnvironment() || _memoryChecker.IsLowResourceEnvironment())
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

        private void AddOrUpdate(TKey key, TValue value, TimeSpan? slidingExpiration)
        {
            if (key == null)
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

        private void UpdateItem(TKey key, TValue value)
        {
            if (key == null)
            {
                throw NullKeyException.Instance;
            }

            var item = _cache[key];
            item.Value = value;
            item.LastAccessed = DateTime.UtcNow;
            item.IncrementAccessCount();
        }

        private bool IsExpired(CacheItem<TValue> item, DateTime now)
        {
            if (item.SlidingExpiration.HasValue && now >= item.LastAccessed + item.SlidingExpiration.Value)
            {
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupCancellationTokenSource.Cancel();
                    _cleanupTask.Wait(TimeSpan.FromSeconds(10)); // Give cleanup task time to finish
                    _cleanupCancellationTokenSource.Dispose();
                    _lock.Dispose();
                }

                _disposed = true;
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConcurrentAdaptiveCache<TKey, TValue>));
            }
        }
    }
}
