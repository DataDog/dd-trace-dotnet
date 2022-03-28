// <copyright file="TagsList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Datadog.Trace.Processors;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Tagging
{
    internal abstract class TagsList : ITags
    {
        // name of string tag dictionary
        private static readonly byte[] MetaBytes = StringEncoding.UTF8.GetBytes("meta");

        // name of numeric tag dictionary
        private static readonly byte[] MetricsBytes = StringEncoding.UTF8.GetBytes("metrics");

        // common tags
        private static readonly byte[] OriginNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);
        private static readonly byte[] RuntimeIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);
        private static readonly byte[] RuntimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);
        private static readonly byte[] LanguageNameBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Language);
        private static readonly byte[] LanguageValueBytes = StringEncoding.UTF8.GetBytes(TracerConstants.Language);
        private static readonly byte[] ProcessIdNameBytes = StringEncoding.UTF8.GetBytes(Trace.Metrics.ProcessId);
        private static readonly byte[] ProcessIdValueBytes;

        private List<KeyValuePair<string, double>> _metrics;
        private List<KeyValuePair<string, string>> _tags;

        static TagsList()
        {
            double processId = DomainMetadata.Instance.ProcessId;
            ProcessIdValueBytes = processId > 0 ? MessagePackSerializer.Serialize(processId) : null;
        }

        protected List<KeyValuePair<string, double>> Metrics => Volatile.Read(ref _metrics);

        protected List<KeyValuePair<string, string>> Tags => Volatile.Read(ref _tags);

        // .

        public virtual string GetTag(string key) => GetTagFromDictionary(key);

        public virtual void SetTag(string key, string value) => SetTagInDictionary(key, value);

        public virtual double? GetMetric(string key) => GetMetricFromDictionary(key);

        public virtual void SetMetric(string key, double? value) => SetMetricInDictionary(key, value);

        // .

        public int SerializeTo(ref byte[] bytes, int offset, Span span, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            offset += WriteTags(ref bytes, offset, span, tagProcessors);
            offset += WriteMetrics(ref bytes, offset, span, tagProcessors);

            return offset - originalOffset;
        }

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            var tags = Tags;

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

            var metrics = Metrics;

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

            WriteAdditionalTags(sb);
            WriteAdditionalMetrics(sb);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        // Tags

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteTag(ref byte[] bytes, ref int offset, string key, string value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteTag(ref byte[] bytes, ref int offset, byte[] keyBytes, string value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMeta(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value);
        }

        private int WriteTags(ref byte[] bytes, int offset, Span span, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            // Start of "meta" dictionary. Do not add any string tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, MetaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // add "language=dotnet" tag to all spans, except those that
            // represents a downstream service or external dependency
            if (this is not InstrumentationTags { SpanKind: SpanKinds.Client or SpanKinds.Producer })
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LanguageNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, LanguageValueBytes);
            }

            // write "custom" span-level string tags (from list of KVPs)
            count += WriteSpanTags(ref bytes, ref offset, Tags, tagProcessors);

            // write "well-known" span-level string tags (from properties)
            count += WriteAdditionalTags(ref bytes, ref offset, tagProcessors);

            if (span.IsRootSpan)
            {
                // write trace-level string tags
                var traceTags = span.Context.TraceContext?.Tags;
                count += WriteTraceTags(ref bytes, ref offset, traceTags, tagProcessors);
            }

            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                // add "runtime-id" tag to service-entry (aka top-level) spans
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, RuntimeIdNameBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, RuntimeIdValueBytes);
            }

            // add "_dd.origin" tag to all spans
            string origin = span.Context.Origin;
            if (!string.IsNullOrEmpty(origin))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, OriginNameBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, origin);
            }

            if (count > 0)
            {
                // Back-patch the count. End of "meta" dictionary. Do not add any string tags after this line.
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }

        private int WriteSpanTags(ref byte[] bytes, ref int offset, List<KeyValuePair<string, string>> tags, ITagProcessor[] tagProcessors)
        {
            int count = 0;

            if (tags != null)
            {
                lock (tags)
                {
                    count += tags.Count;
                    for (var i = 0; i < tags.Count; i++)
                    {
                        var tag = tags[i];
                        WriteTag(ref bytes, ref offset, tag.Key, tag.Value, tagProcessors);
                    }
                }
            }

            return count;
        }

        private int WriteTraceTags(ref byte[] bytes, ref int offset, TraceTagCollection tags, ITagProcessor[] tagProcessors)
        {
            int count = 0;

            if (tags != null)
            {
                lock (tags)
                {
                    count += tags.Count;

                    // don't cast to IEnumerable so we can use the struct enumerator from List<T>
                    foreach (var tag in tags)
                    {
                        WriteTag(ref bytes, ref offset, tag.Key, tag.Value, tagProcessors);
                    }
                }
            }

            return count;
        }

        // Metrics
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteMetric(ref byte[] bytes, ref int offset, string key, double value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteMetric(ref byte[] bytes, ref int offset, byte[] keyBytes, double value, ITagProcessor[] tagProcessors)
        {
            if (tagProcessors is not null)
            {
                string key = null;
                for (var i = 0; i < tagProcessors.Length; i++)
                {
                    tagProcessors[i]?.ProcessMetric(ref key, ref value);
                }
            }

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, keyBytes);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        private int WriteMetrics(ref byte[] bytes, int offset, Span span, ITagProcessor[] tagProcessors)
        {
            int originalOffset = offset;

            // Start of "metrics" dictionary. Do not add any numeric tags before this line.
            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, MetricsBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            // write "custom" span-level numeric tags (from list of KVPs)
            var metrics = Metrics;
            if (metrics != null)
            {
                lock (metrics)
                {
                    count += metrics.Count;
                    for (var i = 0; i < metrics.Count; i++)
                    {
                        var metric = metrics[i];
                        WriteMetric(ref bytes, ref offset, metric.Key, metric.Value, tagProcessors);
                    }
                }
            }

            // write "well-known" span-level numeric tags (from properties)
            count += WriteAdditionalMetrics(ref bytes, ref offset, tagProcessors);

            if (span.IsRootSpan)
            {
                // add "process_id" tag
                if (ProcessIdValueBytes != null)
                {
                    count++;
                    offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, ProcessIdNameBytes);
                    offset += MessagePackBinary.WriteRaw(ref bytes, offset, ProcessIdValueBytes);
                }
            }

            if (span.IsTopLevel && (!Ci.CIVisibility.IsRunning || !Ci.CIVisibility.Settings.Agentless))
            {
                count++;
                WriteMetric(ref bytes, ref offset, Trace.Metrics.TopLevelSpan, 1.0, tagProcessors);
            }

            if (count > 0)
            {
                // Back-patch the count. End of "metrics" dictionary. Do not add any numeric tags after this line.
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }

        // .

        protected virtual int WriteAdditionalTags(ref byte[] bytes, ref int offset, ITagProcessor[] tagProcessors) => 0;

        protected virtual int WriteAdditionalMetrics(ref byte[] bytes, ref int offset, ITagProcessor[] tagProcessors) => 0;

        protected virtual void WriteAdditionalTags(StringBuilder builder)
        {
        }

        protected virtual void WriteAdditionalMetrics(StringBuilder builder)
        {
        }

        private string GetTagFromDictionary(string key)
        {
            var tags = Tags;

            if (tags == null)
            {
                return null;
            }

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

            return null;
        }

        private void SetTagInDictionary(string key, string value)
        {
            var tags = Tags;

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

        private double? GetMetricFromDictionary(string key)
        {
            var metrics = Metrics;

            if (metrics == null)
            {
                return null;
            }

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

            return null;
        }

        private void SetMetricInDictionary(string key, double? value)
        {
            var metrics = Metrics;

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
    }
}
