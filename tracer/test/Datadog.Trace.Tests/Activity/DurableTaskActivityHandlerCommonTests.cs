// <copyright file="DurableTaskActivityHandlerCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

public class DurableTaskActivityHandlerCommonTests
{
    [Theory]
    [InlineData("orchestration", "MyOrchestrator", "orchestration:MyOrchestrator")]
    [InlineData("create_orchestration", "MyOrchestrator", "create_orchestration:MyOrchestrator")]
    [InlineData("activity", "MyActivity", "activity:MyActivity")]
    [InlineData("entity", "MyEntity", "entity:MyEntity")]
    [InlineData("timer", "MyOrchestrator", "MyOrchestrator")]
    [InlineData(null, null, "create_orchestration:MyOrchestrator@1")]
    public void GetResourceName_ReturnsExpectedValue(string? taskType, string? taskName, string expected)
    {
        var result = DurableTaskActivityHandlerCommon.GetResourceName(
            operationName: "create_orchestration:MyOrchestrator@1",
            taskName: taskName,
            taskType: taskType);

        result.Should().Be(expected);
    }

    [Fact]
    public void TryGetTag_FindsMatchingTag()
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(DurableTaskConstants.Tags.Type, "orchestration"),
            new(DurableTaskConstants.Tags.Name, "MyOrchestrator"),
        };

        DurableTaskActivityHandlerCommon.TryGetTag(tags, DurableTaskConstants.Tags.Name, out var value!)
            .Should().BeTrue();
        value.Should().Be("MyOrchestrator");
    }

    [Theory]
    [InlineData(ActivityKind.Server, SpanKinds.Server)]
    [InlineData(ActivityKind.Client, SpanKinds.Client)]
    [InlineData(ActivityKind.Producer, SpanKinds.Producer)]
    [InlineData(ActivityKind.Consumer, SpanKinds.Consumer)]
    [InlineData(ActivityKind.Internal, SpanKinds.Internal)]
    public void MapActivityKindToSpanKind_ReturnsExpectedValue(ActivityKind activityKind, string expected)
    {
        DurableTaskActivityHandlerCommon.MapActivityKindToSpanKind(activityKind).Should().Be(expected);
    }
}
