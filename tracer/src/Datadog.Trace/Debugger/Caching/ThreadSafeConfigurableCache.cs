// <copyright file="ThreadSafeConfigurableCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        /// Most Recently Used
        /// </summary>
        MRU,

        /// <summary>
        /// Least Frequently Used
        /// </summary>
        LFU
    }

    internal class ThreadSafeConfigurableCache<TKey, TValue> : IDisposable
    {
        private const int DefaultCapacity = 2048;
        private const int LowResourceCapacity = 512;
        private const int RestrictedEnvironmentCapacity = 1024;

        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<ThreadSafeConfigurableCache<TKey, TValue>>();

        private readonly Dictionary<TKey, CacheItem> _cache;
        private readonly ReaderWriterLockSlim _lock;
        private readonly EvictionPolicy _evictionPolicy;
        private readonly IEnvironmentChecker _environmentChecker;
        private readonly IMemoryChecker _memoryChecker;
        private long _hits;
        private long _misses;

        internal ThreadSafeConfigurableCache(
            int? capacity = null,
            EvictionPolicy evictionPolicy = EvictionPolicy.LRU,
            IEqualityComparer<TKey> comparer = null,
            IEnvironmentChecker environmentChecker = null,
            IMemoryChecker memoryChecker = null)
        {
            _environmentChecker = environmentChecker ?? new EnvironmentChecker();
            _memoryChecker = memoryChecker ?? new MemoryChecker();
            Capacity = capacity ?? DetermineCapacity();
            Logger.Information("Cache capacity is: {Capacity}", (object)Capacity);
            _cache = new Dictionary<TKey, CacheItem>(Capacity, comparer);
            _lock = new ReaderWriterLockSlim();
            _evictionPolicy = evictionPolicy;
            _hits = 0;
            _misses = 0;
        }

        internal int Capacity { get; }

        internal int Count
        {
            get
            {
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
                long totalRequests = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
                if (totalRequests == 0)
                {
                    return 0;
                }

                return (double)Interlocked.Read(ref _hits) / totalRequests;
            }
        }

        private int DetermineCapacity()
        {
            try
            {
                if (_environmentChecker.IsServerlessEnvironment())
                {
                    return LowResourceCapacity;
                }

                if (_memoryChecker.IsLowResourceEnvironment())
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

        internal void Add(TValue value, params TKey[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterWriteLock();
            try
            {
                foreach (var key in keys)
                {
                    AddOrUpdate(key, value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal void Add(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items == null)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterWriteLock();
            try
            {
                foreach (var item in items)
                {
                    AddOrUpdate(item.Key, item.Value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        internal bool TryGet(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw NullKeyException.Instance;
            }

            _lock.EnterUpgradeableReadLock();

            try
            {
                if (_cache.TryGetValue(key, out CacheItem item))
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        item.LastAccessed = DateTime.UtcNow;
                        item.AccessCount++;
                        Interlocked.Increment(ref _hits);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    value = item.Value;
                    return true;
                }

                Interlocked.Increment(ref _misses);
                value = default(TValue);
                return false;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        internal TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
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
                AddOrUpdate(key, value);
                return value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void AddOrUpdate(TKey key, TValue value)
        {
            if (key == null)
            {
                throw NullKeyException.Instance;
            }

            if (_cache.ContainsKey(key))
            {
                UpdateItem(key, value);
            }
            else
            {
                if (_cache.Count >= Capacity)
                {
                    EvictItem();
                }

                _cache.Add(key, new CacheItem { Value = value, LastAccessed = DateTime.UtcNow, AccessCount = 1 });
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
            item.AccessCount++;
        }

        private void EvictItem()
        {
            switch (_evictionPolicy)
            {
                case EvictionPolicy.LRU:
                    EvictLRU();
                    break;
                case EvictionPolicy.MRU:
                    EvictMRU();
                    break;
                case EvictionPolicy.LFU:
                    EvictLFU();
                    break;
            }
        }

        private void EvictLRU()
        {
            var oldestKey = _cache.OrderBy(kvp => kvp.Value.LastAccessed).First().Key;
            _cache.Remove(oldestKey);
        }

        private void EvictMRU()
        {
            var newestKey = _cache.OrderByDescending(kvp => kvp.Value.LastAccessed).First().Key;
            _cache.Remove(newestKey);
        }

        private void EvictLFU()
        {
            var leastUsedKey = _cache.OrderBy(kvp => kvp.Value.AccessCount).First().Key;
            _cache.Remove(leastUsedKey);
        }

        public void Dispose()
        {
            _lock.Dispose();
        }

        private class CacheItem
        {
            public TValue Value { get; set; }

            public DateTime LastAccessed { get; set; }

            public long AccessCount { get; set; }
        }
    }
}
