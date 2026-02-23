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
using Xunit.Abstractions;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary> FeatureFlagsEvaluator discrete tests </summary>
#pragma warning disable SA1201 // A method should not follow a class
public partial class ExposureCacheTests
{
    public class ExposureEventData
    {
        public string Flag { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Variant { get; set; } = string.Empty;

        public string Allocation { get; set; } = string.Empty;
    }

    public class ExposureCacheTestCase : IXunitSerializable
    {
        public ExposureCacheTestCase()
        {
        }

        public ExposureCacheTestCase(int capacity, SerializableList<ExposureEventData> events, SerializableList<bool> expected, int size)
        {
            Capacity = capacity;
            Events = events;
            Expected = expected;
            Size = size;
        }

        public int Capacity { get; private set; }

        public SerializableList<ExposureEventData> Events { get; private set; } = new();

        public SerializableList<bool> Expected { get; private set; } = new();

        public int Size { get; private set; }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Capacity = info.GetValue<int>(nameof(Capacity));
            Events = info.GetValue<SerializableList<ExposureEventData>>(nameof(Events));
            Expected = info.GetValue<SerializableList<bool>>(nameof(Expected));
            Size = info.GetValue<int>(nameof(Size));
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue(nameof(Capacity), Capacity);
            info.AddValue(nameof(Events), Events);
            info.AddValue(nameof(Expected), Expected);
            info.AddValue(nameof(Size), Size);
        }
    }

    public static TheoryData<ExposureCacheTestCase> Cases()
    {
        var data = new TheoryData<ExposureCacheTestCase>();

        data.Add(new ExposureCacheTestCase(5, [], [], 0));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag", "subject", "variant", "allocation")],
            [true],
            1));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag", "subject", "variant", "allocation"), Event("flag", "subject", "variant", "allocation")],
            [true, false],
            1));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag", "subject", "variant", "allocation"), Event("flag", "subject", "variant", "allocation"), Event("flag1", "subject", "variant", "allocation"), Event("flag1", "subject", "variant", "allocation")],
            [true, false, true, false],
            2));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag", "subject", "variant", "allocation"), Event("flag", "subject", "variant1", "allocation")],
            [true, true],
            1));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag", "subject1", "variant", "allocation"), Event("flag", "subject2", "variant", "allocation"), Event("flag", "subject3", "variant", "allocation")],
            [true, true, true],
            3));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag1", "subject", "variant", "allocation"), Event("flag2", "subject", "variant", "allocation"), Event("flag3", "subject", "variant", "allocation")],
            [true, true, true],
            3));

        data.Add(new ExposureCacheTestCase(
            5,
            [Event("flag1", "subject", "variant", "allocation"), Event("flag2", "subject", "variant", "allocation"), Event("flag3", "subject", "variant", "allocation"), Event("flag4", "subject", "variant", "allocation"), Event("flag5", "subject", "variant", "allocation"), Event("flag6", "subject", "variant", "allocation"), Event("flag7", "subject", "variant", "allocation")],
            [true, true, true, true, true, true, true],
            5));

        return data;

        static ExposureEventData Event(string flag, string subject, string variant, string allocation)
            => new() { Flag = flag, Subject = subject, Variant = variant, Allocation = allocation };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExposureEventsAreAddedOrDiscarded(ExposureCacheTestCase tc)
    {
        var cache = new ExposureCache(tc.Capacity);

        Assert.Equal(tc.Events.Values.Count, tc.Expected.Values.Count);

        for (int x = 0; x < tc.Events.Values.Count; x++)
        {
            var e = tc.Events.Values[x];
            var exposureEvent = CreateEvent(e.Flag, e.Subject, e.Variant, e.Allocation);
            bool added = cache.Add(exposureEvent);
            Assert.Equal(tc.Expected.Values[x], added);
        }

        Assert.Equal(tc.Size, cache.Size);

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
}
#pragma warning restore SA1201 // A method should not follow a class
