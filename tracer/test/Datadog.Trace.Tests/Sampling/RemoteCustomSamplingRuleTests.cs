// <copyright file="RemoteCustomSamplingRuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling;

[Collection(nameof(Datadog.Trace.Tests.Sampling))]
public class RemoteCustomSamplingRuleTests
{
    [Fact]
    public void ConvertToLocalTags()
    {
        var remoteTags = new List<RemoteCustomSamplingRule.RuleConfigJsonModel.TagJsonModel>
        {
            new() { Name = "key1", Value = "value1" },
            new() { Name = "key2", Value = "value2" },
        };

        var localTags = RemoteCustomSamplingRule.ConvertToLocalTags(remoteTags);
        localTags.Should().BeEquivalentTo(new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
    }
}
