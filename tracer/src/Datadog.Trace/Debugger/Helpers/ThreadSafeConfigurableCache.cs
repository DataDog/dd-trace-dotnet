// <copyright file="ThreadSafeConfigurableCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Helpers
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
        private const long LowMemoryThreshold = 1_073_741_824; // 1 GB in bytes

        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<ThreadSafeConfigurableCache<TKey, TValue>>();

        private readonly int _capacity;
        private readonly Dictionary<TKey, CacheItem> _cache;
        private readonly ReaderWriterLockSlim _lock;
        private readonly EvictionPolicy _evictionPolicy;
        private long _hits;
        private long _misses;

        internal ThreadSafeConfigurableCache(int? capacity = null, EvictionPolicy evictionPolicy = EvictionPolicy.LRU)
        {
            _capacity = capacity ?? DetermineCapacity();
            Logger.Information("Cache capacity is: {Capacity}", (object)_capacity);

            _cache = new Dictionary<TKey, CacheItem>(_capacity);
            _lock = new ReaderWriterLockSlim();
            _evictionPolicy = evictionPolicy;
            _hits = 0;
            _misses = 0;
        }

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
                if (IsServerlessEnvironment())
                {
                    return LowResourceCapacity;
                }

                if (IsLowResourceEnvironment())
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

        private bool IsServerlessEnvironment()
        {
            try
            {
                // First we based on the tracer RCM check, this will return true only in a non-serverless environment
                return !Tracer.Instance.Settings.IsRemoteConfigurationAvailable;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Fail to call Tracer.Instance.Settings.IsRemoteConfigurationAvailable");
                // if we failed, test against environment variables for each environment

                // Azure Functions
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")))
                {
                    return true;
                }

                // AWS Lambda
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
                {
                    return true;
                }

                // Google Cloud Functions
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_NAME")))
                {
                    var signatureType = Environment.GetEnvironmentVariable("FUNCTION_SIGNATURE_TYPE");
                    if (signatureType is "http" or "event")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsLowResourceEnvironment()
        {
#if NETCOREAPP3_0_OR_GREATER

            long totalMemory = GC.GetTotalMemory(false);
            long maxGeneration = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

            // Check if we're using more than 75% of available memory or there is less than 1GB of RAM available.
            return totalMemory > (maxGeneration * 0.75) || (FrameworkDescription.Instance.IsWindows() ? CheckWindowsMemory() : CheckUnixMemory());
#else
            return FrameworkDescription.Instance.IsWindows() ? CheckWindowsMemory() : CheckUnixMemory();
#endif
        }

        private bool CheckWindowsMemory()
        {
            try
            {
                WindowsInterop.MEMORYSTATUSEX memStatus = new WindowsInterop.MEMORYSTATUSEX();
                if (WindowsInterop.GlobalMemoryStatusEx(memStatus))
                {
                    // If less than 1GB of RAM is available, consider it a low-resource environment
                    return memStatus.ullAvailPhys < 1_073_741_824; // 1 GB in bytes
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Fail to call GlobalMemoryStatusEx");

                // If we can't access memory info, we'll fall back to the default capacity
                throw;
            }

            return false;
        }

        private bool CheckUnixMemory()
        {
            try
            {
                // for linux we can check /proc/meminfo
                string memInfo = System.IO.File.ReadAllText("/proc/meminfo");
                var memAvailable = memInfo.Split('\n')
                                          .FirstOrDefault(l => l.StartsWith("MemAvailable:"))
                                         ?.Split(':')[1].Trim().Split(' ')[0];
                if (long.TryParse(memAvailable, out long availableKB))
                {
                    return availableKB * 1024 < LowMemoryThreshold;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Fail to read or parse /proc/meminfo");

                // If we can't read memory info, we'll fall back to the default capacity
                throw;
            }

            return false;
        }

        public void Add(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cache.ContainsKey(key))
                {
                    UpdateItem(key, value);
                }
                else
                {
                    if (_cache.Count >= _capacity)
                    {
                        EvictItem();
                    }

                    _cache.Add(key, new CacheItem { Value = value, LastAccessed = DateTime.UtcNow, AccessCount = 1 });
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public TValue Get(TKey key)
        {
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

                    return item.Value;
                }

                Interlocked.Increment(ref _misses);
                throw new KeyNotFoundException($"Key '{key}' not found in cache.");
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        private void UpdateItem(TKey key, TValue value)
        {
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
