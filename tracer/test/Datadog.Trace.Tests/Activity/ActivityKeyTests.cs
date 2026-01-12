// <copyright file="ActivityKeyTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Activity.Handlers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

public class ActivityKeyTests
{
    [Theory]
    [InlineData("spanId", "traceId")]
    [InlineData("id", "")]
    public void Equality(string spanId, string traceId)
    {
        var activity1 = new ActivityKey(traceId: traceId, spanId: spanId);
        var activity2 = new ActivityKey(traceId: traceId, spanId: spanId);
        activity1.Equals(activity2).Should().BeTrue();
        activity1.GetHashCode().Should().Be(activity2.GetHashCode());

        var other = new ActivityKey(traceId: traceId, spanId: "newId");
        activity1.Equals(other).Should().BeFalse();
        activity1.GetHashCode().Should().NotBe(other.GetHashCode());
        activity2.Equals(other).Should().BeFalse();
        activity2.GetHashCode().Should().NotBe(other.GetHashCode());
    }

    [Theory]
    [InlineData("spanId", "traceId")]
    [InlineData("id", "")]
    public void Equality_DefaultComparer(string spanId, string traceId)
    {
        var activity1 = new ActivityKey(traceId: traceId, spanId: spanId);
        var activity2 = new ActivityKey(traceId: traceId, spanId: spanId);

        EqualityComparer<ActivityKey>.Default.Equals(activity1, activity2).Should().BeTrue();
        var hashCode1 = EqualityComparer<ActivityKey>.Default.GetHashCode(activity1).GetHashCode();
        var hashCode2 = EqualityComparer<ActivityKey>.Default.GetHashCode(activity2);
        hashCode1.Should().Be(hashCode2);

        var other = new ActivityKey(traceId: traceId, spanId: "newId");
        var hashCodeOther = EqualityComparer<ActivityKey>.Default.GetHashCode(other).GetHashCode();
        EqualityComparer<ActivityKey>.Default.Equals(activity1, other).Should().BeFalse();
        hashCode1.Should().NotBe(hashCodeOther);
        EqualityComparer<ActivityKey>.Default.Equals(activity2, other).Should().BeFalse();
        hashCode2.Should().NotBe(hashCodeOther);
    }

    [Theory]
    [InlineData("spanId", "traceId", true)]
    [InlineData("spanId", "", true)]
    [InlineData("", "traceId", true)]
    [InlineData("", "", true)]
    [InlineData("spanId", null, false)]
    [InlineData(null, "traceId", false)]
    public void IsValid_SpanId_TraceId(string traceId, string spanId, bool isValid)
    {
        new ActivityKey(traceId: traceId, spanId: spanId).IsValid().Should().Be(isValid);
    }

    [Theory]
    [InlineData("some_id", true)]
    [InlineData("", true)]
    [InlineData(null, false)]
    public void IsValid_SpanId_Id(string id, bool isValid)
    {
        new ActivityKey(id).IsValid().Should().Be(isValid);
    }
}
