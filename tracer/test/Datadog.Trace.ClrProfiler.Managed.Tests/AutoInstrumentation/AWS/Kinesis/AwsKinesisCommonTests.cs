// <copyright file="AwsKinesisCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Kinesis;

public class AwsKinesisCommonTests
{
    public static IEnumerable<object[]> GetStreamNameTestData
        => new List<object[]>
        {
            new object[] { "mystream", string.Empty, "mystream" },                    // Only StreamName set
            new object[] { string.Empty, "arn:aws:kinesis:us-east-2:123456789012:stream/mystream2", "mystream2" }, // Only StreamARN set
            new object[] { "mystream", "arn:aws:kinesis:us-east-2:123456789012:stream/otherstream", "mystream" }, // Both set, StreamName takes precedence
        };

    [Fact]
    public void StreamNameFromARN()
    {
        // It is guaranteed that the last element is going to be the `StreamName`
        const string streamArn = "arn:aws:kinesis:us-east-2:123456789012:stream/mystream";

        AwsKinesisCommon.StreamNameFromARN(streamArn).Should().Be("mystream");

        AwsKinesisCommon.StreamNameFromARN(null).Should().Be(null);

        AwsKinesisCommon.StreamNameFromARN("not-a-stream-arn").Should().Be(null);
    }

    [Theory]
    [MemberData(nameof(GetStreamNameTestData))]
    public void GetStreamName(string streamName, string streamArn, string expected)
    {
        var request = new Mock<IAmazonKinesisRequest>();
        request.Setup(x => x.StreamName).Returns(streamName);
        request.Setup(x => x.StreamArn).Returns(streamArn);

        AwsKinesisCommon.GetStreamName(request.Object).Should().Be(expected);
    }
}
