// <copyright file="MockOtlpResourceSpans.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Trace.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The spans produced by a single resource, within one <see cref="MockOtlpTraceRequest"/>.
/// </summary>
public sealed class MockOtlpResourceSpans
{
    private MockOtlpResourceSpans(MockOtlpResource resource, string schemaUrl, IImmutableList<MockOtlpScopeSpans> scopeSpans)
    {
        Resource = resource;
        SchemaUrl = schemaUrl;
        ScopeSpans = scopeSpans;
    }

    public MockOtlpResource Resource { get; }

    public string SchemaUrl { get; }

    public IImmutableList<MockOtlpScopeSpans> ScopeSpans { get; }

    internal static MockOtlpResourceSpans Create(ResourceSpans resourceSpans)
        => new(
            MockOtlpResource.Create(resourceSpans.Resource),
            resourceSpans.SchemaUrl,
            resourceSpans.ScopeSpans.Select(MockOtlpScopeSpans.Create).ToImmutableList());
}
