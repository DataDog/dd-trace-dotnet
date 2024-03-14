// <copyright file="TagsList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tagging
{
    internal abstract class TagsList : ITags
    {
        protected static readonly Lazy<IDatadogLogger> Logger = new(() => DatadogLogging.GetLoggerFor<TagsList>());
        private List<KeyValuePair<string, string>> _tags;
        private List<KeyValuePair<string, double>> _metrics;
        private List<KeyValuePair<string, byte[]>> _metaStruct;

        public virtual string GetTag(string key)
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

        public virtual void SetTag(string key, string value)
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
                        processor.Process(new TagItem<string>(tags[i].Key, tags[i].Value, null));
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
                    processor.Process(new TagItem<double>(item.Key, item.Value, null));
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

        public virtual void SetMetaStruct(string key, byte[] value)
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
                    processor.Process(new TagItem<byte[]>(item.Key, item.Value, null));
                }
            }
        }

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

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
