// <copyright file="ActivityListenerHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

public class ActivityListenerHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListensToEverySourceWithTheSameName(bool hasVersion)
    {
        var name = $"Datadog.Tests.{Guid.NewGuid():N}";
        var first = CreateSource(name, hasVersion);
        var second = CreateSource(name, hasVersion);

        ActivityListenerHandler.OnShouldListenTo(first).Should().BeTrue();
        ActivityListenerHandler.OnShouldListenTo(second).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListensToConcurrentSourcesWithTheSameName(bool hasVersion)
    {
        var name = $"Datadog.Tests.{Guid.NewGuid():N}";
        var sources = new ISource[32];
        var results = new bool[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            sources[i] = CreateSource(name, hasVersion);
        }

        Parallel.For(0, sources.Length, i => results[i] = ActivityListenerHandler.OnShouldListenTo(sources[i]));

        results.Should().OnlyContain(listens => listens);
    }

    private static ISource CreateSource(string name, bool hasVersion)
    {
        if (hasVersion)
        {
            var source = new Mock<IActivitySource>();
            source.SetupGet(x => x.Name).Returns(name);
            source.SetupGet(x => x.Version).Returns("1.0.0");
            return source.Object;
        }

        var legacySource = new Mock<ISource>();
        legacySource.SetupGet(x => x.Name).Returns(name);
        return legacySource.Object;
    }
}
