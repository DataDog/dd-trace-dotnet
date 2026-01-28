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
    [Fact]
    public void TagsPresentWhenEnabled()
    {
        var processTags = new ProcessTags(serviceNameUserDefined: true);
        var tags = processTags.SerializedTags;

        tags.Should().ContainAll(ProcessTags.EntrypointBasedir, ProcessTags.EntrypointWorkdir, ProcessTags.EntrypointName, ProcessTags.ServiceNameUserDefined);
        // EntrypointName may not be present, especially when ran in the CI

        tags.Split(',').Should().BeInAscendingOrder();

        // assert on the format, which is key:values in a string, comma separated.
        tags.Split(',')
            .Should()
            .AllSatisfy(s =>
            {
                s.Count(c => c == ':').Should().Be(1);
            });

        tags.Should().Contain("service_name.user_defined:true");
        // cannot really assert the rest of the content because it depends on how the tests are run.
    }
}
