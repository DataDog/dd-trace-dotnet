// <copyright file="ActivityTagProjection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Shared projection of a Datadog <see cref="Span"/>'s tag/metric storage into the shape the
    /// intercepted Activity tag-read APIs (get_Tags, get_TagObjects, GetTagItem, EnumerateTagObjects)
    /// expect. Centralising this in one place means all four APIs can never disagree with each other.
    /// </summary>
    /// <remarks>
    /// Covers, in order:
    /// 1. <see cref="ITags.EnumerateTags{TProcessor}"/> — ordinary string/int tags.
    /// 2. <see cref="ITags.EnumerateMetrics{TProcessor}"/> — numeric attributes. <c>OtlpHelpers.SetTagObject</c>
    ///    routes every numeric CLR type to <c>Span.SetMetric</c> rather than <c>SetTag</c>, so without this
    ///    step <c>activity.SetTag("size", 1024)</c> round-trips to nothing on the read side.
    /// 3. The reserved OTel keys that <c>OtlpHelpers.AgentSetOtlpTag</c> diverts into span fields instead of
    ///    storing as tags (operation.name, service.name, resource.name, span.type,
    ///    http.response.status_code). Re-emitted here from the span fields so they are visible again.
    ///
    /// Caveat on the reserved keys: <c>Span.OperationName</c> / <c>ServiceName</c> / <c>ResourceName</c> /
    /// <c>Type</c> all have fallback derivations that run whether or not the corresponding tag was ever
    /// explicitly set (see the resource-name / operation-name / span-type fallbacks in
    /// <c>ActivityStopIntegration</c>). So these four may be re-emitted here even when the customer never
    /// called e.g. <c>activity.SetTag("operation.name", ...)</c> — there is no cheap way to distinguish
    /// "explicitly tag-set" from "defaulted" without extra per-span bookkeeping, which is out of scope for
    /// this pass. <c>http.response.status_code</c> does not have this problem: <see cref="Tags.HttpStatusCode"/>
    /// is only ever written via this same remap, so it is only re-emitted when the user actually set it.
    ///
    /// Type fidelity is deliberately not attempted here: numerics come back as the <see cref="double"/>
    /// they were stored as via <c>SetMetric</c>, not their original CLR type. Full fidelity requires
    /// changing the write path (letting span tag storage hold the original <c>object</c>), which is a
    /// separate, later change; this projection defines the read shape that write-path change would need
    /// to keep serving.
    /// </remarks>
    internal static class ActivityTagProjection
    {
        internal static void Project<TProcessor>(Span span, ref TProcessor processor)
            where TProcessor : struct, IItemProcessor<string>, IItemProcessor<int>, IItemProcessor<double>
        {
            span.Tags.EnumerateTags(ref processor, span.OpenTelemetrySemanticsEnabled);
            span.Tags.EnumerateMetrics(ref processor);
            ProjectReservedKeys(span, ref processor);
        }

        /// <summary>
        /// Convenience wrapper over <see cref="Project{TProcessor}"/> for the two read APIs that need an
        /// object-valued <c>List&lt;KeyValuePair&lt;string, object?&gt;&gt;</c>: <c>get_TagObjects</c> and
        /// (as the source list for the synthesised <c>DiagNode</c> chain) <c>EnumerateTagObjects</c>.
        /// </summary>
        internal static List<KeyValuePair<string, object?>> ProjectToObjectList(Span span)
        {
            var list = new List<KeyValuePair<string, object?>>();
            var processor = new ObjectTagListBuilder(list);
            Project(span, ref processor);
            return list;
        }

        /// <summary>
        /// Keyed lookup for <c>Activity.GetTagItem(string)</c> (DS 7+). Checks, in order: the Span's
        /// tags, its metrics, then the reserved-key span fields — mirroring <see cref="Project{TProcessor}"/>
        /// but without materialising a full projection for a single key.
        /// </summary>
        internal static object? GetTagItem(Span span, string key)
        {
            if (span.GetTag(key) is { } tagValue)
            {
                return tagValue;
            }

            if (span.Tags.GetMetric(key) is { } metricValue)
            {
                return metricValue;
            }

            return key switch
            {
                "operation.name" => span.OperationName is { Length: > 0 } opName ? opName : null,
                "service.name" => span.ServiceName is { Length: > 0 } svcName ? svcName : null,
                "resource.name" => span.ResourceName is { Length: > 0 } resName ? resName : null,
                "span.type" => span.Type is { Length: > 0 } type ? type : null,
                "http.response.status_code" => span.GetTag(Tags.HttpStatusCode) is { Length: > 0 } statusCode ? statusCode : null,
                _ => null
            };
        }

        /// <summary>
        /// Re-emits the reserved OTel keys from their span fields, for the full-projection getters
        /// (<c>get_Tags</c>, <c>get_TagObjects</c>, <c>EnumerateTagObjects</c>). The keyed lookup
        /// <see cref="GetTagItem"/> checks the same fields directly instead of calling this.
        /// </summary>
        internal static void ProjectReservedKeys<TProcessor>(Span span, ref TProcessor processor)
            where TProcessor : struct, IItemProcessor<string>
        {
            if (span.OperationName is { Length: > 0 } operationName)
            {
                processor.Process(new TagItem<string>("operation.name", operationName, default));
            }

            if (span.ServiceName is { Length: > 0 } serviceName)
            {
                processor.Process(new TagItem<string>("service.name", serviceName, default));
            }

            if (span.ResourceName is { Length: > 0 } resourceName)
            {
                processor.Process(new TagItem<string>("resource.name", resourceName, default));
            }

            if (span.Type is { Length: > 0 } spanType)
            {
                processor.Process(new TagItem<string>("span.type", spanType, default));
            }

            if (span.GetTag(Tags.HttpStatusCode) is { Length: > 0 } statusCode)
            {
                processor.Process(new TagItem<string>("http.response.status_code", statusCode, default));
            }
        }

        private struct ObjectTagListBuilder : IItemProcessor<string>, IItemProcessor<int>, IItemProcessor<double>
        {
            private readonly List<KeyValuePair<string, object?>> _list;

            public ObjectTagListBuilder(List<KeyValuePair<string, object?>> list) => _list = list;

            public void Process(TagItem<string> item) => _list.Add(new KeyValuePair<string, object?>(item.Key, item.Value));

            public void Process(TagItem<int> item) => _list.Add(new KeyValuePair<string, object?>(item.Key, item.Value));

            public void Process(TagItem<double> item) => _list.Add(new KeyValuePair<string, object?>(item.Key, item.Value));
        }
    }
}
