// <copyright file="IW3CActivity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity.DuckTypes
{
    internal interface IW3CActivity : IActivity
    {
        [DuckField(Name = "_traceId")]
        string TraceId { get; set; }

        [DuckField(Name = "_spanId")]
        string SpanId { get; set; }

        [DuckField(Name = "_parentSpanId")]
        string ParentSpanId { get; set; }

        [DuckField(Name = "_id")]
        string? RawId { get; set; }

        [DuckField(Name = "_parentId")]
        string? RawParentId { get; set; }
    }
}
