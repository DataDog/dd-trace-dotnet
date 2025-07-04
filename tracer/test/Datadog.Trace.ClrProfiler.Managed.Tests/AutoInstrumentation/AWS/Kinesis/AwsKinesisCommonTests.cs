// <copyright file="AwsKinesisCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.Kinesis;

public class AwsKinesisCommonTests
{
    public static IEnumerable<object[]> GetStreamNameTestData
        => new List<object[]>
        {
            new object[] { "mystream", string.Empty, "mystream" },
            new object[] { string.Empty, "arn:aws:kinesis:us-east-2:123456789012:stream/mystream2", "mystream2" },
            new object[] { "streamname", "arn:aws:kinesis:us-east-2:123456789012:stream/streamarn", "streamarn" },
        };

    [Fact]
    public void StreamNameFromARN()
    {
        const string streamArn = "arn:aws:kinesis:us-east-2:123456789012:stream/mystream";

        AwsKinesisCommon.StreamNameFromARN(streamArn).Should().Be("mystream");

        AwsKinesisCommon.StreamNameFromARN(null).Should().Be(null);

        AwsKinesisCommon.StreamNameFromARN("not-a-stream-arn").Should().Be(null);
    }

    [Theory]
    [MemberData(nameof(GetStreamNameTestData))]
    public void GetStreamName(string streamName, string streamArn, string expected)
    {
        var request = new Mock<IAmazonKinesisRequestWithStreamNameAndStreamArn>();
        request.Setup(x => x.StreamName).Returns(streamName);
        request.Setup(x => x.StreamARN).Returns(streamArn);

        AwsKinesisCommon.GetStreamName(request.Object).Should().Be(expected);
    }
}
