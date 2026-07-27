// <copyright file="SnapshotSegmentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Debugger.Configurations.Models;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class SnapshotSegmentTests
{
    [Fact]
    public void EquivalentJsonWithDifferentPropertyOrder_EqualsAndGetHashCodeAreConsistent()
    {
        // JToken.DeepEquals ignores JObject property order, but JObject.ToString preserves it.
        // Hashing on the serialized form would produce different hash codes for equal segments,
        // violating the Equals/GetHashCode contract. Locks in that GetHashCode does not depend
        // on the Json serialization order.
        var first = new SnapshotSegment(dsl: "expr", json: @"{""a"":1,""b"":2}", str: "x");
        var second = new SnapshotSegment(dsl: "expr", json: @"{""b"":2,""a"":1}", str: "x");

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
