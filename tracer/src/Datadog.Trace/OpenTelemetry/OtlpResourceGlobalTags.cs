// <copyright file="OtlpResourceGlobalTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.OpenTelemetry;

/// <summary>
/// The global tags (<c>DD_TAGS</c>, leftover <c>OTEL_RESOURCE_ATTRIBUTES</c>, or their remote config
/// <c>tracing_tags</c> override) that a settings snapshot writes once as OTLP resource attributes,
/// instead of repeating them as attributes on every span.
/// </summary>
/// <remarks>
/// Instances are immutable and cached per <see cref="Configuration.MutableSettings.GlobalTags"/> dictionary,
/// which is replaced (never mutated) when the settings change, so filtering and sorting run once per snapshot
/// and the per-span check is a single dictionary lookup.
/// </remarks>
internal sealed class OtlpResourceGlobalTags
{
    private static readonly ConditionalWeakTable<ReadOnlyDictionary<string, string>, OtlpResourceGlobalTags> Cache = new();

    // Resource attributes the tracer writes itself (see OtlpMapper.EmitResourceAttributesFromTraceChunk), or that
    // OtlpMapper.IsHandledResourceAttribute reserves. A global tag must never override them.
    private static readonly HashSet<string> TracerResourceKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "telemetry.sdk.name",
        "telemetry.sdk.language",
        "telemetry.sdk.version",
        Ci.Tags.CommonTags.GitCommit,
        Ci.Tags.CommonTags.GitRepository,
        Tags.RuntimeId,
        Tags.SdkSemantics,
    };

    // Keys that Span.SetTag consumes into span fields, metrics, or trace-level decisions instead of storing them
    // as a plain span tag, or that only make sense per span. They keep their current per-span handling.
    private static readonly HashSet<string> SpanLevelKeys = new(StringComparer.Ordinal)
    {
        Tags.SamplingPriority,
        Tags.ManualKeep,
        Tags.ManualDrop,
        Tags.PeerService,
        Tags.ErrorMsg,
        Tags.ErrorStack,
        Tags.SpanKind,
        Tags.InstrumentationName,
        "otel.status_code",
        "otel.status_description",
        "http-client-handler-type",
    };

    // Keys backed by a strongly-typed ITags property that a span doesn't export as the same string attribute:
    // - properties with both a Datadog and an OpenTelemetry name ([Tag(..., OtelName = ...)]): the span exports
    //   whichever name DD_TRACE_OTEL_SEMANTICS_ENABLED selects, so it can't be matched against the resource key;
    // - non-string properties (e.g. int "server.port"): the span exports a number, not a string.
    // These keep their current per-span export. Both spellings are listed because SetTag accepts either one.
    // OtlpResourceAttributesTests.TypedTagKeys_AreNotWrittenToTheResource fails if a new such property is added.
    private static readonly HashSet<string> TypedTagKeys = new(StringComparer.Ordinal)
    {
        Tags.HttpMethod,
        Tags.HttpRequestMethod,
        Tags.HttpUrl,
        Tags.UrlFull,
        Tags.HttpStatusCode,
        Tags.HttpResponseStatusCode,
        Tags.OutHost,
        Tags.ServerAddress,
        Tags.ServerPort,
        Tags.HttpUserAgent,
        Tags.UserAgentOriginal,
        Tags.HttpClientIp,
        Tags.ClientAddress,
        Tags.NetworkClientIp,
        Tags.NetworkPeerAddress,
    };

    // never mutated after construction
    private readonly Dictionary<string, string> _lookup;

    private OtlpResourceGlobalTags(ReadOnlyDictionary<string, string> globalTags)
    {
        var lookup = new Dictionary<string, string>(globalTags.Count, StringComparer.Ordinal);

        foreach (var pair in globalTags)
        {
            if (IsResourceAttribute(pair.Key))
            {
                lookup[pair.Key] = pair.Value;
            }
        }

        var attributes = new KeyValuePair<string, string>[lookup.Count];
        var i = 0;
        foreach (var pair in lookup)
        {
            attributes[i++] = pair;
        }

        // sort by key so the resource is written in a stable order
        Array.Sort(attributes, static (x, y) => string.CompareOrdinal(x.Key, y.Key));

        _lookup = lookup;
        Attributes = attributes;
    }

    /// <summary>
    /// Gets the global tags to write as resource attributes, sorted by key.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; }

    /// <summary>
    /// Gets the resource global tags of the settings snapshot that the trace chunk's trace was created with
    /// (captured by its <see cref="TraceContext"/>), not the tracer's latest settings, so traces keep the
    /// global tags they were created with after a remote configuration change.
    /// Returns <c>null</c> when none are written to the resource.
    /// </summary>
    public static OtlpResourceGlobalTags? FromTraceChunk(in TraceChunkModel traceChunk)
    {
        if (traceChunk.SpanCount == 0)
        {
            return null;
        }

        // all spans in a chunk belong to the same trace, use the first span's trace context like TraceChunkModel does
        return Get(traceChunk.GetSpanModel(0).Span.Context.TraceContext?.CurrentTraceSettings?.Settings.GlobalTags);
    }

    /// <summary>
    /// Returns the cached instance for a settings snapshot's global tags,
    /// or <c>null</c> when none of them are written to the resource.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static OtlpResourceGlobalTags? Get(ReadOnlyDictionary<string, string>? globalTags)
    {
        if (globalTags is not { Count: > 0 })
        {
            return null;
        }

        var resourceGlobalTags = Cache.GetValue(globalTags, static tags => new OtlpResourceGlobalTags(tags));
        return resourceGlobalTags.Attributes.Count > 0 ? resourceGlobalTags : null;
    }

    /// <summary>
    /// Returns <c>true</c> when two snapshots write the same global tags to the resource.
    /// Snapshots usually share the same instance, so the content comparison only runs after
    /// the settings were replaced.
    /// </summary>
    public static bool AreEquivalent(OtlpResourceGlobalTags? x, OtlpResourceGlobalTags? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x._lookup.Count != y._lookup.Count)
        {
            return false;
        }

        foreach (var pair in x._lookup)
        {
            if (!y._lookup.TryGetValue(pair.Key, out var value) || !string.Equals(pair.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> when the resource carries this exact key and value, in which case the
    /// span attribute is redundant and can be omitted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string? key, string? value)
        => key is not null && _lookup.TryGetValue(key, out var resourceValue) && string.Equals(resourceValue, value, StringComparison.Ordinal);

    /// <summary>
    /// Returns <c>true</c> when a global tag is written as a resource attribute (and omitted from spans).
    /// </summary>
    [TestingAndPrivateOnly]
    internal static bool IsResourceAttribute(string? key)
    {
        if (StringUtil.IsNullOrEmpty(key))
        {
            return false;
        }

        if (OtlpMapper.IsHandledResourceAttribute(key) || TracerResourceKeys.Contains(key) || SpanLevelKeys.Contains(key) || TypedTagKeys.Contains(key))
        {
            return false;
        }

        // Datadog-internal tags ("_dd.origin", "_dd.measured", "_dd1.sr.eausr", ...) and Azure App Service tags
        // are set or consumed by the tracer itself.
        if (key.StartsWith("_dd", StringComparison.Ordinal) || key.StartsWith("aas.", StringComparison.Ordinal))
        {
            return false;
        }

        // No length check: the tag truncator never runs when exporting OTLP, and resource attributes
        // are exempt from attribute limits (https://opentelemetry.io/docs/specs/otel/common/#exempt-entities).
        return true;
    }
}
