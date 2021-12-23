// <copyright file="TagsList.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Tagging
{
    internal abstract class TagsList : ITags
    {
        private static byte[] _metaBytes = StringEncoding.UTF8.GetBytes("meta");
        private static byte[] _metricsBytes = StringEncoding.UTF8.GetBytes("metrics");
        private static byte[] _originBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.Origin);
        private static byte[] _runtimeIdBytes = StringEncoding.UTF8.GetBytes(Trace.Tags.RuntimeId);
        private static byte[] _runtimeIdValueBytes = StringEncoding.UTF8.GetBytes(Tracer.RuntimeId);

        private List<KeyValuePair<string, double>> _metrics;
        private List<KeyValuePair<string, string>> _tags;

        protected List<KeyValuePair<string, double>> Metrics => Volatile.Read(ref _metrics);

        protected List<KeyValuePair<string, string>> Tags => Volatile.Read(ref _tags);

        public string GetTag(string key)
        {
            foreach (var property in GetAdditionalTags())
            {
                if (property.Key == key)
                {
                    return property.Getter(this);
                }
            }

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

        public double? GetMetric(string key)
        {
            foreach (var property in GetAdditionalMetrics())
            {
                if (property.Key == key)
                {
                    return property.Getter(this);
                }
            }

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

        public void SetTag(string key, string value)
        {
            foreach (var property in GetAdditionalTags())
            {
                if (property.Key == key)
                {
                    property.Setter(this, value);
                    return;
                }
            }

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

        public void SetMetric(string key, double? value)
        {
            foreach (var property in GetAdditionalMetrics())
            {
                if (property.Key == key)
                {
                    property.Setter(this, value);
                    return;
                }
            }

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

        public int SerializeTo(ref byte[] bytes, int offset, Span span)
        {
            int originalOffset = offset;

            offset += WriteTags(ref bytes, offset, span);
            offset += WriteMetrics(ref bytes, offset, span);

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

            foreach (var property in GetAdditionalTags())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    sb.Append($"{property.Key} (tag):{value},");
                }
            }

            foreach (var property in GetAdditionalMetrics())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    sb.Append($"{property.Key} (metric):{value.Value},");
                }
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        protected virtual IProperty<string>[] GetAdditionalTags() => Array.Empty<IProperty<string>>();

        protected virtual IProperty<double?>[] GetAdditionalMetrics() => Array.Empty<IProperty<double?>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteTag(ref byte[] bytes, ref int offset, string key, string value)
        {
            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteString(ref bytes, offset, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteMetric(ref byte[] bytes, ref int offset, string key, double value)
        {
            offset += MessagePackBinary.WriteString(ref bytes, offset, key);
            offset += MessagePackBinary.WriteDouble(ref bytes, offset, value);
        }

        private int WriteTags(ref byte[] bytes, int offset, Span span)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metaBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            var tags = Tags;

            bool isOriginWritten = false;

            if (tags != null)
            {
                lock (tags)
                {
                    count += tags.Count;

                    foreach (var pair in tags)
                    {
                        WriteTag(ref bytes, ref offset, pair.Key, pair.Value);
                    }
                }
            }

            foreach (var property in GetAdditionalTags())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    if (property.Key == Trace.Tags.Origin)
                    {
                        isOriginWritten = true;
                    }

                    count++;
                    WriteTag(ref bytes, ref offset, property.Key, value);
                }
            }

            if (span.IsTopLevel)
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdBytes);
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _runtimeIdValueBytes);
            }

            string origin = span.Context.Origin;
            if (!isOriginWritten && !string.IsNullOrEmpty(origin))
            {
                count++;
                offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _originBytes);
                offset += MessagePackBinary.WriteString(ref bytes, offset, origin);
            }

            if (count > 0)
            {
                // Back-patch the count
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }

        private int WriteMetrics(ref byte[] bytes, int offset, Span span)
        {
            int originalOffset = offset;

            offset += MessagePackBinary.WriteStringBytes(ref bytes, offset, _metricsBytes);

            int count = 0;

            // We don't know the final count yet, write a fixed-size header and note the offset
            var countOffset = offset;
            offset += MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, offset, 0);

            var metrics = Metrics;

            if (metrics != null)
            {
                lock (metrics)
                {
                    count += metrics.Count;

                    foreach (var pair in metrics)
                    {
                        WriteMetric(ref bytes, ref offset, pair.Key, pair.Value);
                    }
                }
            }

            foreach (var property in GetAdditionalMetrics())
            {
                var value = property.Getter(this);

                if (value != null)
                {
                    count++;
                    WriteMetric(ref bytes, ref offset, property.Key, value.Value);
                }
            }

            if (span.IsTopLevel)
            {
                count++;
                WriteMetric(ref bytes, ref offset, Trace.Metrics.TopLevelSpan, 1.0);
            }

            if (count > 0)
            {
                // Back-patch the count
                MessagePackBinary.WriteMapHeaderForceMap32Block(ref bytes, countOffset, (uint)count);
            }

            return offset - originalOffset;
        }
    }
}
