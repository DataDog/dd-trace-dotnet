// <copyright file="TagsList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal class TagsList : ITags
    {
        protected static readonly Lazy<IDatadogLogger> Logger = new(() => DatadogLogging.GetLoggerFor<TagsList>());
        private List<KeyValuePair<string, string>>? _tags;
        private List<KeyValuePair<string, double>>? _metrics;
        private List<KeyValuePair<string, byte[]>>? _metaStruct;

        private static int CountNotNull(string? value) => value is null ? 0 : 1;

        private static void EnsureAdditionalCapacity<T>(List<KeyValuePair<string, T>> list, int additionalCountUpperBound)
        {
            if (additionalCountUpperBound <= 0)
            {
                return;
            }

            var requiredCapacity = list.Count + additionalCountUpperBound;
            if (list.Capacity < requiredCapacity)
            {
                list.Capacity = requiredCapacity;
            }
        }

        private List<KeyValuePair<string, string>> GetOrCreateTagsList(int additionalCountUpperBound)
        {
            var tags = Volatile.Read(ref _tags);

            if (tags is null)
            {
                var newTags = additionalCountUpperBound > 0
                                  ? new List<KeyValuePair<string, string>>(additionalCountUpperBound)
                                  : new List<KeyValuePair<string, string>>();

                tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
            }

            return tags;
        }

        public virtual void SetTag(string key, string? value)
        {
            // Avoid allocating the tags list when the operation is effectively a no-op
            // (removing a tag from an uninitialized list).
            var tags = Volatile.Read(ref _tags);
            if (value is null)
            {
                if (tags is null)
                {
                    return;
                }
            }
            else
            {
                tags = GetOrCreateTagsList(additionalCountUpperBound: 1);
            }

            lock (tags)
            {
                SetTagNoLock(tags, key, value);
            }
        }

        /// <summary>
        /// Sets multiple tags.
        /// Uses the same semantics as <see cref="SetTag"/> for each tag (replace/remove existing keys).
        /// </summary>
        public virtual void SetTags(
            string key1,
            string? value1,
            string key2,
            string? value2,
            string key3,
            string? value3)
        {
            var additionalCountUpperBound =
                CountNotNull(value1) +
                CountNotNull(value2) +
                CountNotNull(value3);

            var tags = GetOrCreateTagsList(additionalCountUpperBound);

            lock (tags)
            {
                EnsureAdditionalCapacity(tags, additionalCountUpperBound);

                SetTagNoLock(tags, key1, value1);
                SetTagNoLock(tags, key2, value2);
                SetTagNoLock(tags, key3, value3);
            }
        }

        /// <summary>
        /// Sets multiple tags.
        /// Uses the same semantics as <see cref="SetTag"/> for each tag (replace/remove existing keys).
        /// </summary>
        public virtual void SetTags(
            string key1,
            string? value1,
            string key2,
            string? value2,
            string key3,
            string? value3,
            string key4,
            string? value4)
        {
            var additionalCountUpperBound =
                CountNotNull(value1) +
                CountNotNull(value2) +
                CountNotNull(value3) +
                CountNotNull(value4);

            var tags = GetOrCreateTagsList(additionalCountUpperBound);

            lock (tags)
            {
                EnsureAdditionalCapacity(tags, additionalCountUpperBound);

                SetTagNoLock(tags, key1, value1);
                SetTagNoLock(tags, key2, value2);
                SetTagNoLock(tags, key3, value3);
                SetTagNoLock(tags, key4, value4);
            }
        }

        /// <summary>
        /// Sets multiple tags.
        /// Uses the same semantics as <see cref="SetTag"/> for each tag (replace/remove existing keys).
        /// </summary>
        public virtual void SetTags(
            string key1,
            string? value1,
            string key2,
            string? value2,
            string key3,
            string? value3,
            string key4,
            string? value4,
            string key5,
            string? value5,
            string key6,
            string? value6,
            string key7,
            string? value7)
        {
            var additionalCountUpperBound =
                CountNotNull(value1) +
                CountNotNull(value2) +
                CountNotNull(value3) +
                CountNotNull(value4) +
                CountNotNull(value5) +
                CountNotNull(value6) +
                CountNotNull(value7);

            var tags = GetOrCreateTagsList(additionalCountUpperBound);

            lock (tags)
            {
                EnsureAdditionalCapacity(tags, additionalCountUpperBound);

                SetTagNoLock(tags, key1, value1);
                SetTagNoLock(tags, key2, value2);
                SetTagNoLock(tags, key3, value3);
                SetTagNoLock(tags, key4, value4);
                SetTagNoLock(tags, key5, value5);
                SetTagNoLock(tags, key6, value6);
                SetTagNoLock(tags, key7, value7);
            }
        }

        /// <summary>
        /// NOTE: This method mutates the underlying list and is intentionally lock-free.
        /// Callers MUST hold the lock on the specific `tags` instance for the duration of the call.
        /// See <see cref="SetTag"/> and SetTags overloads.
        /// </summary>
        private static void SetTagNoLock(List<KeyValuePair<string, string>> tags, string key, string? value)
        {
            for (var i = 0; i < tags.Count; i++)
            {
                if (tags[i].Key == key)
                {
                    if (value is null)
                    {
                        tags.RemoveAt(i);
                    }
                    else
                    {
                        tags[i] = new KeyValuePair<string, string>(key, value);
                    }

                    return;
                }
            }

            if (value is not null)
            {
                tags.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        public virtual string? GetTag(string key)
        {
            var tags = Volatile.Read(ref _tags);
            if (tags is not null)
            {
                lock (tags)
                {
                    for (int i = 0; i < tags.Count; i++)
                    {
                        if (tags[i].Key == key)
                        {
                            return tags[i].Value;
                        }
                    }
                }
            }

            return null;
        }

        public virtual void EnumerateTags<TProcessor>(ref TProcessor processor)
            where TProcessor : struct, IItemProcessor<string>
        {
            var tags = Volatile.Read(ref _tags);
            if (tags is not null)
            {
                lock (tags)
                {
                    for (int i = 0; i < tags.Count; i++)
                    {
                        processor.Process(new TagItem<string>(tags[i].Key, tags[i].Value, default));
                    }
                }
            }
        }

        public virtual double? GetMetric(string key)
        {
            var metrics = Volatile.Read(ref _metrics);
            if (metrics is not null)
            {
                lock (metrics)
                {
                    for (int i = 0; i < metrics.Count; i++)
                    {
                        if (metrics[i].Key == key)
                        {
                            return metrics[i].Value;
                        }
                    }
                }
            }

            return null;
        }

        public virtual void SetMetric(string key, double? value)
        {
            var metrics = Volatile.Read(ref _metrics);

            if (metrics == null)
            {
                var newMetrics = new List<KeyValuePair<string, double>>();
                metrics = Interlocked.CompareExchange(ref _metrics, newMetrics, null) ?? newMetrics;
            }

            lock (metrics)
            {
                for (int i = 0; i < metrics.Count; i++)
                {
                    if (metrics[i].Key == key)
                    {
                        if (value == null)
                        {
                            metrics.RemoveAt(i);
                        }
                        else
                        {
                            metrics[i] = new KeyValuePair<string, double>(key, value.Value);
                        }

                        return;
                    }
                }

                // If we get there, the tag wasn't in the collection
                if (value != null)
                {
                    metrics.Add(new KeyValuePair<string, double>(key, value.Value));
                }
            }
        }

        public virtual void EnumerateMetrics<TProcessor>(ref TProcessor processor)
            where TProcessor : struct, IItemProcessor<double>
        {
            var metrics = Volatile.Read(ref _metrics);
            if (metrics is null)
            {
                return;
            }

            lock (metrics)
            {
                for (var i = 0; i < metrics.Count; i++)
                {
                    var item = metrics[i];
                    processor.Process(new TagItem<double>(item.Key, item.Value, default));
                }
            }
        }

        public virtual bool HasMetaStruct()
        {
            var metastruct = Volatile.Read(ref _metaStruct);
            if (metastruct is not null)
            {
                lock (metastruct)
                {
                    return metastruct.Count > 0;
                }
            }

            return false;
        }

        public virtual void SetMetaStruct(string key, byte[]? value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                var metastruct = Volatile.Read(ref _metaStruct);

                if (metastruct == null)
                {
                    var newMetastruct = new List<KeyValuePair<string, byte[]>>();
                    metastruct = Interlocked.CompareExchange(ref _metaStruct, newMetastruct, null) ?? newMetastruct;
                }

                lock (metastruct)
                {
                    for (int i = 0; i < metastruct.Count; i++)
                    {
                        if (metastruct[i].Key == key)
                        {
                            if (value == null)
                            {
                                metastruct.RemoveAt(i);
                            }
                            else
                            {
                                metastruct[i] = new KeyValuePair<string, byte[]>(key, value);
                            }

                            return;
                        }
                    }

                    // If we get there, the key wasn't in the collection
                    if (value != null)
                    {
                        metastruct.Add(new KeyValuePair<string, byte[]>(key, value));
                    }
                }
            }
        }

        public virtual void EnumerateMetaStruct<TProcessor>(ref TProcessor processor)
            where TProcessor : struct, IItemProcessor<byte[]>
        {
            var metastruct = Volatile.Read(ref _metaStruct);
            if (metastruct is null)
            {
                return;
            }

            lock (metastruct)
            {
                for (var i = 0; i < metastruct.Count; i++)
                {
                    var item = metastruct[i];
                    processor.Process(new TagItem<byte[]>(item.Key, item.Value, default));
                }
            }
        }

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire();

            var tags = Volatile.Read(ref _tags);

            if (tags != null)
            {
                lock (tags)
                {
                    foreach (var pair in tags)
                    {
                        sb.Append($"{pair.Key} (tag):{pair.Value},");
                    }
                }
            }

            var metrics = Volatile.Read(ref _metrics);

            if (metrics != null)
            {
                lock (metrics)
                {
                    foreach (var pair in metrics)
                    {
                        sb.Append($"{pair.Key} (metric):{pair.Value}");
                    }
                }
            }

            var metaStruct = Volatile.Read(ref _metaStruct);

            if (metaStruct != null)
            {
                lock (metaStruct)
                {
                    foreach (var pair in metaStruct)
                    {
                        sb.Append($"{pair.Key} (metaStruct)");
                    }
                }
            }

            WriteAdditionalTags(sb);
            WriteAdditionalMetrics(sb);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        protected virtual void WriteAdditionalTags(StringBuilder builder)
        {
        }

        protected virtual void WriteAdditionalMetrics(StringBuilder builder)
        {
        }
    }
}
