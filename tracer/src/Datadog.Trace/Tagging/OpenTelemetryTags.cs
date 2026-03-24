// <copyright file="OpenTelemetryTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class OpenTelemetryTags : TagsList
    {
        [Tag(Tags.SpanKind)]
        public virtual string? SpanKind { get; set; }

        [Tag("otel.trace_id")]
        public virtual string? OtelTraceId { get; set; }

        [Tag("otel.library.name")]
        public virtual string? OtelLibraryName { get; set; }

        [Tag("otel.library.version")]
        public virtual string? OtelLibraryVersion { get; set; }

        [Tag("otel.status_code")]
        public virtual string? OtelStatusCode { get; set; }
    }
}
