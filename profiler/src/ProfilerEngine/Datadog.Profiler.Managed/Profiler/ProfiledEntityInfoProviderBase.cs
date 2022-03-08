// <copyright file="ProfiledEntityInfoProviderBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Datadog.Profiler
{
    /// <summary>
    /// Samples need to contain info about entities in the context of which the samples were collected.
    /// For example, Thread or AppDomain info.
    /// We need to strike a balance between displaying the information that was current when the sample was
    /// collected (e.g. thread names can be set) and the overhead of fetching and storing that info.
    /// We do that by refreshing the info once per Profiles Export Session.
    ///
    /// This class stores info about contextual entities (Threads, AppDomains, ...) in a cache.
    /// It knows when to fetch new data from the runtime and when to use data readily fetched during the
    /// current export session.
    /// When data for a particular entity cannot be fetched, it implies that the respective entity no longer
    /// exists (Thread destroyed, AddDomain unloaded, ...). In such cases, the last known data is kept in
    /// cache for several export sessions. This is becasue we may still encounter samples that were collected
    /// while the entity existed and carry that context.
    /// The cache is regularly compacted to throw away stale entities.
    /// </summary>
    internal abstract class ProfiledEntityInfoProviderBase<TEntityId, TEntityInfo> : IDisposable
        where TEntityInfo : ProfiledEntityInfoBase
    {
        private readonly int _cacheCompactionTrigger_AfterSessions;
        private readonly int _cacheCompactionTrigger_WhenCacheGrewBy;

        private readonly Dictionary<TEntityId, TEntityInfo> _cache;
        private int _sessionId;
        private int _cacheSizeAfterLastCompaction;

        public ProfiledEntityInfoProviderBase(int cacheCompactionTrigger_AfterSessions, int cacheCompactionTrigger_WhenCacheGrewBy)
        {
            if (cacheCompactionTrigger_AfterSessions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheCompactionTrigger_AfterSessions));
            }

            if (cacheCompactionTrigger_WhenCacheGrewBy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheCompactionTrigger_WhenCacheGrewBy));
            }

            _cacheCompactionTrigger_AfterSessions = cacheCompactionTrigger_AfterSessions;
            _cacheCompactionTrigger_WhenCacheGrewBy = cacheCompactionTrigger_WhenCacheGrewBy;

            _cache = new Dictionary<TEntityId, TEntityInfo>();

            _sessionId = 0;
            _cacheSizeAfterLastCompaction = 0;
        }

        public int SessionId
        {
            get { return _sessionId; }
        }

        public int StartNextSession()
        {
            if (_sessionId == int.MaxValue)
            {
                _sessionId = 0;
            }
            else
            {
                ++_sessionId;
            }

            if (_sessionId % _cacheCompactionTrigger_AfterSessions == 0
                    || (_cache.Count - _cacheSizeAfterLastCompaction) > _cacheCompactionTrigger_WhenCacheGrewBy)
            {
                PerformCacheCompaction();
            }

            return _sessionId;
        }

        public bool TryGetProfiledEntityInfo(TEntityId profiledEntityInfoId, out TEntityInfo entityInfo)
        {
            if (_cache.TryGetValue(profiledEntityInfoId, out entityInfo))
            {
                // IF last time we used this entity it was inactive THEN it is still inative and we assume that cached data has not changed.
                // IF last time we used this entity it was active AND it was during the current session THEN we use the cached data.

                // IF last time we used this entity it was active AND it was during a different session THEN we need to refresh the cached data.

                if (entityInfo.IsEntityActive && entityInfo.ProviderSessionId != _sessionId)
                {
                    // Try refreshing cached data. If we cannot, then the entity is no longer active.
                    if (!TryGetEntityInfoFromNative(profiledEntityInfoId, ref entityInfo))
                    {
                        entityInfo.SetEntityInactive(_sessionId);
                    }
                }

                // Refreshed or not, the data was in cache and can be used.
                return true;
            }

            // Data was not in cache. Try getting it from native and put it into the cache.

            entityInfo = null;
            if (TryGetEntityInfoFromNative(profiledEntityInfoId, ref entityInfo))
            {
                _cache.Add(profiledEntityInfoId, entityInfo);
                return true;
            }

            // Could not get the data. Give up.

            return false;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        protected abstract bool TryGetEntityInfoFromNative(TEntityId profiledEntityInfoId, ref TEntityInfo entityInfo);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cache.Clear();
            }
        }

        private void PerformCacheCompaction()
        {
            var staleIDs = new List<TEntityId>(capacity: Math.Min(_cache.Count, 500));
            foreach (KeyValuePair<TEntityId, TEntityInfo> entry in _cache)
            {
                if (entry.Value.IsEntityStale)
                {
                    // If this entity info was determined to be inactive in a previous compaction phase, we are unlikely to see it again.
                    // Remove it from the cache.
                    // Note, seeing this entity info is very unlikely, but not impossible in some rare concurrency scenarios.
                    // In those cases we will miss including the respective entity info into the data.

                    staleIDs.Add(entry.Key);
                }
                else
                {
                    // For all entities not already known as inactive, check if they are became inactive now.
                    // However, do not delete them from cache as the info may still be requested.
                    // Deletion will occur during the next compaction.
                    TEntityInfo entityInfo = entry.Value;
                    if (entityInfo.IsEntityActive && !TryGetEntityInfoFromNative(entry.Key, ref entityInfo))
                    {
                        entityInfo.SetEntityInactive(_sessionId);
                    }
                }
            }

            for (int i = 0; i < staleIDs.Count; i++)
            {
                _cache.Remove(staleIDs[i]);
            }

            _cacheSizeAfterLastCompaction = _cache.Count;
        }
    }
}
