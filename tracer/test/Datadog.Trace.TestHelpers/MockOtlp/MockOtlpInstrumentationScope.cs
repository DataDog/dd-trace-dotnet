// <copyright file="MockOtlpInstrumentationScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Common.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The instrumentation scope (name/version/attributes) that produced a <see cref="MockOtlpScopeSpans"/>.
/// </summary>
public sealed class MockOtlpInstrumentationScope
{
    private MockOtlpInstrumentationScope(string name, string version, IImmutableList<MockOtlpAttribute> attributes, uint droppedAttributesCount)
    {
        Name = name;
        Version = version;
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }

    public string Name { get; }

    public string Version { get; }

    public IImmutableList<MockOtlpAttribute> Attributes { get; }

    public uint DroppedAttributesCount { get; }

    internal static MockOtlpInstrumentationScope Create(InstrumentationScope scope)
        => scope is null
               ? null
               : new MockOtlpInstrumentationScope(scope.Name, scope.Version, scope.Attributes.Select(MockOtlpAttribute.Create).ToImmutableList(), scope.DroppedAttributesCount);
}
