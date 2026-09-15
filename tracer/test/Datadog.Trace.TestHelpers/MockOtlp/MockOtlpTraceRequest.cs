// <copyright file="MockOtlpTraceRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// One decoded OTLP/HTTP <c>/v1/traces</c> request (<c>ExportTraceServiceRequest</c>), preserving the
/// resource/instrumentation-scope envelope structure.
/// </summary>
public sealed class MockOtlpTraceRequest
{
    private MockOtlpTraceRequest(ExportTraceServiceRequest raw, IImmutableList<MockOtlpResourceSpans> resourceSpans)
    {
        Raw = raw;
        ResourceSpans = resourceSpans;
    }

    /// <summary>
    /// Gets the underlying protobuf message this request was decoded from, regardless of whether the
    /// request arrived as JSON or protobuf on the wire. Useful for re-serializing to the OTLP JSON wire
    /// format for tooling that expects it (e.g. snapshot comparisons).
    /// </summary>
    public ExportTraceServiceRequest Raw { get; }

    public IImmutableList<MockOtlpResourceSpans> ResourceSpans { get; }

    /// <summary>
    /// Gets every span across every resource/scope in this request, in a flat list.
    /// </summary>
    public IImmutableList<MockOtlpSpan> Spans
        => ResourceSpans.SelectMany(rs => rs.ScopeSpans).SelectMany(ss => ss.Spans).ToImmutableList();

    internal static MockOtlpTraceRequest Create(ExportTraceServiceRequest request)
        => new(request, request.ResourceSpans.Select(MockOtlpResourceSpans.Create).ToImmutableList());
}
