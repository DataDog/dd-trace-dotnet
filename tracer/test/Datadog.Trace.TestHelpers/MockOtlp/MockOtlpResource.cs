// <copyright file="MockOtlpResource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using OpenTelemetry.Proto.Resource.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The resource that produced a <see cref="MockOtlpResourceSpans"/>.
/// </summary>
public sealed class MockOtlpResource
{
    private MockOtlpResource(IImmutableList<MockOtlpAttribute> attributes, uint droppedAttributesCount)
    {
        Attributes = attributes;
        DroppedAttributesCount = droppedAttributesCount;
    }

    public IImmutableList<MockOtlpAttribute> Attributes { get; }

    public uint DroppedAttributesCount { get; }

    internal static MockOtlpResource Create(Resource resource)
        => resource is null
               ? null
               : new MockOtlpResource(resource.Attributes.Select(MockOtlpAttribute.Create).ToImmutableList(), resource.DroppedAttributesCount);
}
