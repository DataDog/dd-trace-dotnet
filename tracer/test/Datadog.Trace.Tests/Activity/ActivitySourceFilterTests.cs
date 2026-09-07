// <copyright file="ActivitySourceFilterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

/// <summary>
/// Pins <see cref="ActivitySourceFilter.ShouldIgnore"/> (the CallTarget interception path) against
/// <see cref="IgnoreActivityHandler.SourcesNames"/> (the managed ActivityListener path). The two used to be
/// independently-maintained arrays that had already drifted apart; <see cref="ActivitySourceFilter"/> now
/// reads <see cref="IgnoreActivityHandler.SourcesNames"/> directly, but this test protects against a future
/// change reintroducing a second, divergent list.
/// </summary>
public class ActivitySourceFilterTests
{
    public static IEnumerable<object[]> IgnoredSourceNames()
    {
        foreach (var name in IgnoreActivityHandler.SourcesNames)
        {
            yield return new object[] { name };
        }
    }

    [Theory]
    [MemberData(nameof(IgnoredSourceNames))]
    public void ShouldIgnore_MatchesEveryEntryInIgnoreActivityHandlerSourcesNames(string sourceName)
    {
        ActivitySourceFilter.ShouldIgnore(sourceName, version: null).Should().BeTrue();
    }

    [Fact]
    public void ShouldIgnore_UnrelatedSourceIsNotIgnored()
    {
        ActivitySourceFilter.ShouldIgnore("SomeCompletelyUnrelatedActivitySource", version: null).Should().BeFalse();
    }
}
