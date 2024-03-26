// <copyright file="AwsSqsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class AwsSqsCommonTests
{
    public static IEnumerable<object[]> SchemaSpanKindOperationNameData
        => new List<object[]>
        {
            new object[] { "v0", "client", "sqs.request" },
            new object[] { "v0", "producer", "sqs.request" },
            new object[] { "v1", "client", "aws.sqs.request" },
            new object[] { "v1", "producer", "aws.sqs.send" },
            new object[] { "v1", "consumer", "aws.sqs.process" },
            new object[] { "v1", "server", "aws.sqs.request" }
        };

    [Fact]
    public void GetCorrectQueueName()
    {
        // It is guaranteed that the last element is going to be the `QueueName`
        var queueUrl = "https://localhost:8080/00000000/my-queue-name";

        AwsSqsCommon.GetQueueName(queueUrl).Should().Be("my-queue-name");

        queueUrl = null;
        // When the request does not contain a `QueueUrl` it should return `null`
        AwsSqsCommon.GetQueueName(queueUrl).Should().Be(null);
    }

    [Theory]
    [MemberData(nameof(SchemaSpanKindOperationNameData))]
    public void GetCorrectOperationName(string schemaVersion, string spanKind, string expected)
    {
        var tracer = GetTracer(schemaVersion);

        AwsSqsCommon.GetOperationName(tracer, spanKind).Should().Be(expected);
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
