// <copyright file="ProcessTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests;

public class ProcessTagsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TagsPresentWhenServiceNameUserDefined(bool isServiceNameUserDefined)
    {
        var processTags = new ProcessTags(serviceNameUserDefined: isServiceNameUserDefined, autoServiceName: "auto-service");
        var tags = processTags.SerializedTags;

        tags.Should().ContainAll(ProcessTags.EntrypointBasedir, ProcessTags.EntrypointWorkdir);
        // EntrypointName may not be present, especially when ran in the CI

        tags.Split(',').Should().BeInAscendingOrder();

        // assert on the format, which is key:values in a string, comma separated.
        tags.Split(',')
            .Should()
            .AllSatisfy(s =>
            {
                s.Count(c => c == ':').Should().Be(1);
            });

        if (isServiceNameUserDefined)
        {
            tags.Should().Contain("svc.user:1");
            tags.Should().NotContain("svc.auto");
        }
        else
        {
            tags.Should().NotContain("svc.user");
            tags.Should().Contain("svc.auto:auto-service");
        }

        // cannot really assert the rest of the content because it depends on how the tests are run.
    }
}
