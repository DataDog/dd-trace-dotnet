// <copyright file="AllocationFreeEnumeratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.Activity.Helpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity.Helpers;

public class AllocationFreeEnumeratorTests
{
    [Fact]
    public void CanEnumerateActivityTags()
    {
        var activity = new System.Diagnostics.Activity("operation");
        activity.AddTag("key", "value");
        activity.AddTag("key2", "value2");

        var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string>>, KeyValuePair<string, string>, int>
           .BuildAllocationFreeForEachDelegate(activity.Tags.GetType());

        int invocations = 0;
        forEach(
            activity.Tags,
            ref invocations,
            (ref state, item) =>
            {
                state++;
                return true;
            });

        invocations.Should().Be(2);
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void CanEnumerateActivityTagObjects()
    {
        var activity = new System.Diagnostics.Activity("operation");
        activity.AddTag("key", 23);
        activity.AddTag("key2", "value2");

        var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object>>, KeyValuePair<string, object>, int>
           .BuildAllocationFreeForEachDelegate(activity.TagObjects.GetType());

        int invocations = 0;
        forEach(
            activity.TagObjects,
            ref invocations,
            (ref state, item) =>
            {
                state++;
                return true;
            });

        invocations.Should().Be(2);
    }
#endif
}
