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

        /// <summary>
        /// Begin a batch add operation for tags.
        /// This is intended for internal hot paths that add multiple tags at once.
        /// The returned <see cref="TagBatch"/> holds the underlying tag list lock and MUST be disposed (use <c>using</c>).
        /// Callers should keep the critical section small: do not perform any slow/allocating work while holding the batch.
        /// The batch is append-only: it does not perform key lookups and will not replace existing keys.
        /// Callers are responsible for ensuring keys are not already present (or guarding under the same lock).
        /// </summary>
        internal TagBatch BeginTagBatch(int additionalTagCount)
        {
            if (additionalTagCount < 0)
            {
                additionalTagCount = 0;
            }

            var tags = Volatile.Read(ref _tags);

            if (tags == null)
            {
                // Use a capacity that matches what the caller expects to add to avoid internal growth allocations when adding multiple tags.
                var newTags = new List<KeyValuePair<string, string>>(additionalTagCount);
                tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
            }

            Monitor.Enter(tags);

            try
            {
                var requiredCapacity = tags.Count + additionalTagCount;
                if (tags.Capacity < requiredCapacity)
                {
                    tags.Capacity = requiredCapacity;
                }

                return new TagBatch(tags);
            }
            catch
            {
                Monitor.Exit(tags);
                throw;
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

        public virtual void SetTag(string key, string? value)
        {
            var tags = Volatile.Read(ref _tags);

            if (tags == null)
            {
                var newTags = new List<KeyValuePair<string, string>>();
                tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
            }

            lock (tags)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (tags[i].Key == key)
                    {
                        if (value == null)
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

                // If we get there, the tag wasn't in the collection
                if (value != null)
                {
                    tags.Add(new KeyValuePair<string, string>(key, value));
                }
            }
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

        /// <summary>
        /// A lock-holding batch helper for appending tags.
        /// Note: this is a <see langword="struct"/> that contains a reference. Do not copy it, and dispose exactly once.
        /// </summary>
        internal readonly struct TagBatch : IDisposable
        {
            private readonly List<KeyValuePair<string, string>>? _tags;

            internal TagBatch(List<KeyValuePair<string, string>> tags)
            {
                _tags = tags;
            }

            /// <summary>
            /// Checks if the tag list already contains the specified key.
            /// This is intended for internal race-avoidance while the batch lock is held.
            /// </summary>
            public bool ContainsKey(string key)
            {
                var tags = _tags;
                if (tags is null)
                {
                    ThrowHelper.ThrowInvalidOperationException("TagBatch is not initialized. It must be created via TagsList.BeginTagBatch().");
                }

                for (var i = 0; i < tags.Count; i++)
                {
                    if (tags[i].Key == key)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Adds a tag without performing a key lookup.
            /// Callers must ensure the key is not already present (or handle duplicates explicitly).
            /// </summary>
            public void Add(string key, string value)
            {
                var tags = _tags;
                if (tags is null)
                {
                    ThrowHelper.ThrowInvalidOperationException("TagBatch is not initialized. It must be created via TagsList.BeginTagBatch().");
                }

                tags.Add(new KeyValuePair<string, string>(key, value));
            }

            public void Dispose()
            {
                // Make default(TagBatch).Dispose() a no-op.
                // Misuse cases like double-dispose or disposing on the wrong thread should still throw.
                if (_tags is { } tags)
                {
                    Monitor.Exit(tags);
                }
            }
        }
    }
}
