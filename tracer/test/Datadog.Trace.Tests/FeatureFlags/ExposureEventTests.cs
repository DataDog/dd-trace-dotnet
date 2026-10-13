// <copyright file="ExposureEventTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class ExposureEventTests
{
    [Theory]
    [InlineData(340132)]
    [InlineData(0)]
    public void SerializesSerialIdAsATopLevelIntegerField(long serialId)
    {
        var serialized = Serialize(CreateEvent(serialId));

        serialized.Value<long?>("serial_id").Should().Be(serialId);
    }

    [Fact]
    public void OmitsSerialIdWhenTheSplitCarriesNone()
    {
        var serialized = Serialize(CreateEvent(null));

        serialized.ContainsKey("serial_id").Should().BeFalse();
    }

    private static JObject Serialize(ExposureEvent exposureEvent)
    {
        return JObject.Parse(JsonConvert.SerializeObject(exposureEvent, ExposureApi.SerializerSettings));
    }

    private static ExposureEvent CreateEvent(long? serialId)
    {
        return new ExposureEvent(
            1755000000000,
            new Trace.FeatureFlags.Exposure.Model.Allocation("allocation-a"),
            new Trace.FeatureFlags.Exposure.Model.Flag("test-flag"),
            new Trace.FeatureFlags.Exposure.Model.Variant("variant-a"),
            new Subject("user-123", new Dictionary<string, object?>()),
            serialId);
    }
}
