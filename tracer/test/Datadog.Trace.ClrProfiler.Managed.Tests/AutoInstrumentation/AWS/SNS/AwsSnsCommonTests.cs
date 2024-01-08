// <copyright file="AwsSnsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.SNS;

public class AwsSnsCommonTests
{
    public static IEnumerable<object[]> SchemaSpanKindOperationNameData
        => new List<object[]>
        {
            new object[] { "v0", "client", "sns.request" },
            new object[] { "v0", "producer", "sns.request" },
            new object[] { "v1", "client", "aws.sns.request" },
            new object[] { "v1", "producer", "aws.sns.send" },
            new object[] { "v1", "consumer", "aws.sns.process" },
            new object[] { "v1", "server", "aws.sns.request" }
        };

    [Fact]
    public void GetCorrectTopicName()
    {
        // It is guaranteed that the last element is going to be the `QueueName`
        const string topicArn = "arn:aws:sns:region:000000000:my-topic-name";

        AwsSnsCommon.GetTopicName(topicArn).Should().Be("my-topic-name");

        // When the request does not contain a `TopicArn` it should return `null`
        AwsSnsCommon.GetTopicName(null).Should().Be(null);
    }

    [Theory]
    [MemberData(nameof(SchemaSpanKindOperationNameData))]
    public void GetCorrectOperationName(string schemaVersion, string spanKind, string expected)
    {
        var tracer = GetTracer(schemaVersion);

        AwsSnsCommon.GetOperationName(tracer, spanKind).Should().Be(expected);
    }

    private static Tracer GetTracer(string schemaVersion)
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
    }
}
