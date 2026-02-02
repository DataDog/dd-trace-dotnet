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
        private Dictionary<string, string>? _tags;
        private Dictionary<string, double>? _metrics;
        private Dictionary<string, byte[]>? _metaStruct;

        public virtual string? GetTag(string key)
        {
            var tags = Volatile.Read(ref _tags);
            if (tags is not null)
            {
                lock (tags)
                {
                    if (tags.TryGetValue(key, out var value))
                    {
                        return value;
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
                var newTags = new Dictionary<string, string>();
                tags = Interlocked.CompareExchange(ref _tags, newTags, null) ?? newTags;
            }

            lock (tags)
            {
                if (value == null)
                {
                    tags.Remove(key);
                }
                else
                {
                    tags[key] = value;
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
                    foreach (var kvp in tags)
                    {
                        processor.Process(new TagItem<string>(kvp.Key, kvp.Value, default));
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
                    if (metrics.TryGetValue(key, out var value))
                    {
                        return value;
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
                var newMetrics = new Dictionary<string, double>();
                metrics = Interlocked.CompareExchange(ref _metrics, newMetrics, null) ?? newMetrics;
            }

            lock (metrics)
            {
                if (value == null)
                {
                    metrics.Remove(key);
                }
                else
                {
                    metrics[key] = value.Value;
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
                foreach (var kvp in metrics)
                {
                    processor.Process(new TagItem<double>(kvp.Key, kvp.Value, default));
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
                    var newMetastruct = new Dictionary<string, byte[]>();
                    metastruct = Interlocked.CompareExchange(ref _metaStruct, newMetastruct, null) ?? newMetastruct;
                }

                lock (metastruct)
                {
                    if (value == null)
                    {
                        metastruct.Remove(key);
                    }
                    else
                    {
                        metastruct[key] = value;
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
                foreach (var kvp in metastruct)
                {
                    processor.Process(new TagItem<byte[]>(kvp.Key, kvp.Value, default));
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
                        sb.Append(pair.Key).Append(" (tag):").Append(pair.Value).Append(',');
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
                        sb.Append(pair.Key).Append(" (metric):").Append(pair.Value);
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
                        sb.Append(pair.Key).Append(" (metaStruct)");
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
