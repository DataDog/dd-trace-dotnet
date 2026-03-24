// <copyright file="ExposureCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using Xunit;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> FeatureFlagsEvaluator discrete tests </summary>
public partial class ExposureCacheTests
{
    public static IEnumerable<object?[]> Cases()
    {
        yield return new object?[]
        {
            5,
            new object[]
            {
            },
            new bool[]
            {
            },
            0
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag", "subject", "variant", "allocation"),
            },
            new bool[]
            {
                true,
            },
            1
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag", "subject", "variant", "allocation"),
                CreateEvent("flag", "subject", "variant", "allocation"),
            },
            new bool[]
            {
                true,
                false,
            },
            1
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag", "subject", "variant", "allocation"),
                CreateEvent("flag", "subject", "variant", "allocation"),
                CreateEvent("flag1", "subject", "variant", "allocation"),
                CreateEvent("flag1", "subject", "variant", "allocation"),
            },
            new bool[]
            {
                true,
                false,
                true,
                false,
            },
            2
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag", "subject", "variant", "allocation"),
                CreateEvent("flag", "subject", "variant1", "allocation"),
            },
            new bool[]
            {
                true,
                true,
            },
            1
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag", "subject1", "variant", "allocation"),
                CreateEvent("flag", "subject2", "variant", "allocation"),
                CreateEvent("flag", "subject3", "variant", "allocation"),
            },
            new bool[]
            {
                true,
                true,
                true,
            },
            3
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag1", "subject", "variant", "allocation"),
                CreateEvent("flag2", "subject", "variant", "allocation"),
                CreateEvent("flag3", "subject", "variant", "allocation"),
            },
            new bool[]
            {
                true,
                true,
                true,
            },
            3
        };
        yield return new object?[]
        {
            5,
            new object[]
            {
                CreateEvent("flag1", "subject", "variant", "allocation"),
                CreateEvent("flag2", "subject", "variant", "allocation"),
                CreateEvent("flag3", "subject", "variant", "allocation"),
                CreateEvent("flag4", "subject", "variant", "allocation"),
                CreateEvent("flag5", "subject", "variant", "allocation"),
                CreateEvent("flag6", "subject", "variant", "allocation"),
                CreateEvent("flag7", "subject", "variant", "allocation"),
            },
            new bool[]
            {
                true,
                true,
                true,
                true,
                true,
                true,
                true,
            },
            5
        };

        static ExposureEvent CreateEvent(string flag, string subject, string variant, string allocation)
        {
            return new ExposureEvent(
              new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds(),
              new Trace.FeatureFlags.Exposure.Model.Allocation(allocation),
              new Trace.FeatureFlags.Exposure.Model.Flag(flag),
              new Trace.FeatureFlags.Exposure.Model.Variant(variant),
              new Subject(subject, new Dictionary<string, object?>()));
      }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExposureEventsAreAddedOrDiscarded(int capacity, object[] exposureEvents, bool[] expected, int size)
    {
        var cache = new ExposureCache(capacity);

        Assert.Equal(exposureEvents.Length, expected.Length);

        for (int x = 0; x < exposureEvents.Length; x++)
        {
            bool added = cache.Add((ExposureEvent)exposureEvents[x]);
            Assert.Equal(expected[x], added);
        }

        Assert.Equal(size, cache.Size);
    }
}
