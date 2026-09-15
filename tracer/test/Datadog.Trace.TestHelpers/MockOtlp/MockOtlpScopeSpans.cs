// <copyright file="MockOtlpScopeSpans.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Trace.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The spans produced by a single instrumentation scope, within one <see cref="MockOtlpResourceSpans"/>.
/// </summary>
public sealed class MockOtlpScopeSpans
{
    private MockOtlpScopeSpans(MockOtlpInstrumentationScope scope, string schemaUrl, IImmutableList<MockOtlpSpan> spans)
    {
        Scope = scope;
        SchemaUrl = schemaUrl;
        Spans = spans;
    }

    public MockOtlpInstrumentationScope Scope { get; }

    public string SchemaUrl { get; }

    public IImmutableList<MockOtlpSpan> Spans { get; }

    internal static MockOtlpScopeSpans Create(ScopeSpans scopeSpans)
        => new(
            MockOtlpInstrumentationScope.Create(scopeSpans.Scope),
            scopeSpans.SchemaUrl,
            scopeSpans.Spans.Select(MockOtlpSpan.Create).ToImmutableList());
}
