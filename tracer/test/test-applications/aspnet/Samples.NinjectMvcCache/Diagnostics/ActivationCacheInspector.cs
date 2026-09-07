using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ninject;
using Ninject.Activation.Caching;
using Ninject.Components;

namespace Samples.NinjectMvcCache.Diagnostics
{
    internal static class ActivationCacheInspector
    {
        public static object Capture(IKernel kernel)
        {
            var cache = kernel.Components.Get<IActivationCache>();
            if (cache is null || cache.GetType().Name == "NoOpActivationCache")
            {
                return new
                {
                    CacheType = cache?.GetType().FullName,
                    Disabled = true,
                };
            }

            var activatedObjectsField = cache.GetType().GetField("activatedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            var activatedObjects = activatedObjectsField?.GetValue(cache);
            if (activatedObjects is null)
            {
                return new
                {
                    CacheType = cache.GetType().FullName,
                    Error = "The activation-cache layout did not match Ninject 3.0.",
                };
            }

            lock (activatedObjects)
            {
                return CaptureLocked(cache, activatedObjects);
            }
        }

        private static object CaptureLocked(IActivationCache cache, object activatedObjects)
        {
            var setType = activatedObjects.GetType();
            var count = (int)setType.GetProperty("Count").GetValue(activatedObjects, null);
            var buckets = (Array)setType.GetField("m_buckets", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(activatedObjects);
            var slots = (Array)setType.GetField("m_slots", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(activatedObjects);
            var version = (int)(setType.GetField("m_version", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(activatedObjects) ?? 0);

            var hashCounts = new Dictionary<int, int>();
            var targetTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var item in (IEnumerable)activatedObjects)
            {
                if (item is null)
                {
                    continue;
                }

                var itemType = item.GetType();
                var hashField = itemType.GetField("cashedHashCode", BindingFlags.Instance | BindingFlags.NonPublic);
                var weakReferenceField = itemType.GetField("weakReference", BindingFlags.Instance | BindingFlags.NonPublic);
                var hash = (int)(hashField?.GetValue(item) ?? item.GetHashCode());
                hashCounts[hash] = hashCounts.TryGetValue(hash, out var currentHashCount) ? currentHashCount + 1 : 1;

                var target = (weakReferenceField?.GetValue(item) as WeakReference)?.Target;
                if (target is not null)
                {
                    var targetType = target.GetType().FullName;
                    targetTypeCounts[targetType] = targetTypeCounts.TryGetValue(targetType, out var currentTypeCount) ? currentTypeCount + 1 : 1;
                }
            }

            var largestHashGroup = hashCounts.OrderByDescending(pair => pair.Value).FirstOrDefault();
            var requiredAttributeCount = targetTypeCounts.TryGetValue(
                "System.ComponentModel.DataAnnotations.RequiredAttribute",
                out var requiredCount)
                    ? requiredCount
                    : 0;

            return new
            {
                CacheType = cache.GetType().FullName,
                Disabled = false,
                Count = count,
                Capacity = buckets?.Length ?? slots?.Length ?? 0,
                Version = version,
                DistinctHashes = hashCounts.Count,
                LargestHash = largestHashGroup.Key,
                LargestHashGroup = largestHashGroup.Value,
                LargestHashGroupPercentage = count == 0 ? 0 : (100.0 * largestHashGroup.Value) / count,
                RequiredAttributeCount = requiredAttributeCount,
                TopTargetTypes = targetTypeCounts
                                .OrderByDescending(pair => pair.Value)
                                .Take(10)
                                .Select(pair => new { Type = pair.Key, pair.Value })
                                .ToArray(),
            };
        }
    }
}
