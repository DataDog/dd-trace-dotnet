// <copyright file="OtlpResourceIdentity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.OpenTelemetry;

/// <summary>
/// The inputs of <see cref="OtlpMapper.EmitResourceAttributesFromTraceChunk{TState}"/> for one trace chunk.
/// The OTLP trace serializers keep one per <c>ResourceSpans</c> and open a new <c>ResourceSpans</c> when a chunk
/// doesn't match the current one, so chunks that produce different resource attributes never share a resource.
/// </summary>
/// <remarks>
/// Where each input comes from:
/// <list type="bullet">
/// <item><description>Global tags: the settings snapshot the trace was created with (<see cref="TraceContext.CurrentTraceSettings"/>),
/// so they can differ between traces after a remote configuration change.</description></item>
/// <item><description>Environment and service version: read from the trace's <see cref="TraceContext"/> when the chunk is serialized
/// (initialized from the trace's settings snapshot, and updated if a span sets the <c>env</c> or <c>version</c> tag).</description></item>
/// <item><description>Default service name: the tracer's current settings when the chunk is serialized.</description></item>
/// <item><description>OpenTelemetry semantics and client-computed stats: tracer-wide, fixed at startup.</description></item>
/// <item><description>Git metadata: tracer-wide, but may be resolved lazily after startup.</description></item>
/// </list>
/// </remarks>
internal sealed class OtlpResourceIdentity
{
    private readonly string? _defaultServiceName;
    private readonly string? _environment;
    private readonly string? _serviceVersion;
    private readonly string? _gitCommitSha;
    private readonly string? _gitRepositoryUrl;
    private readonly bool _otelSemanticsEnabled;
    private readonly bool _clientComputedStats;
    private readonly OtlpResourceGlobalTags? _globalTags;

    public OtlpResourceIdentity(in TraceChunkModel traceChunk, OtlpResourceGlobalTags? globalTags)
    {
        _defaultServiceName = traceChunk.DefaultServiceName;
        _environment = traceChunk.Environment;
        _serviceVersion = traceChunk.ServiceVersion;
        _gitCommitSha = traceChunk.GitCommitSha;
        _gitRepositoryUrl = traceChunk.GitRepositoryUrl;
        _otelSemanticsEnabled = traceChunk.OtelSemanticsEnabled;
        _clientComputedStats = traceChunk.ClientComputedStats;
        _globalTags = globalTags;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="traceChunk"/> produces the same resource attributes,
    /// so its spans can be written under the same <c>ResourceSpans</c>.
    /// </summary>
    public bool Matches(in TraceChunkModel traceChunk, OtlpResourceGlobalTags? globalTags)
        => string.Equals(_defaultServiceName, traceChunk.DefaultServiceName, StringComparison.Ordinal) &&
           string.Equals(_environment, traceChunk.Environment, StringComparison.Ordinal) &&
           string.Equals(_serviceVersion, traceChunk.ServiceVersion, StringComparison.Ordinal) &&
           string.Equals(_gitCommitSha, traceChunk.GitCommitSha, StringComparison.Ordinal) &&
           string.Equals(_gitRepositoryUrl, traceChunk.GitRepositoryUrl, StringComparison.Ordinal) &&
           _otelSemanticsEnabled == traceChunk.OtelSemanticsEnabled &&
           _clientComputedStats == traceChunk.ClientComputedStats &&
           OtlpResourceGlobalTags.AreEquivalent(_globalTags, globalTags);
}
