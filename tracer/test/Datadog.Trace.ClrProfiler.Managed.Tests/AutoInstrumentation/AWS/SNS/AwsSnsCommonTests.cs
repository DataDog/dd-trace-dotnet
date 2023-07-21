// <copyright file="AwsSnsCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNSTests;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS;

public class AwsSnsCommonTests
{
    private readonly SpanContext _spanContext;

    public AwsSnsCommonTests()
    {
        ulong upper = 1234567890123456789;
        ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 0, "test-service", "origin");
    }

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
        var topicArn = "arn:aws:sns:region:000000000:my-topic-name";

        AwsSnsCommon.GetTopicName(topicArn).Should().Be("my-topic-name");

        topicArn = null;
        // When the request does not contain a `TopicArn` it should return `null`
        AwsSnsCommon.GetTopicName(topicArn).Should().Be(null);
    }

    [Fact]
    public void InjectHeadersIntoMessage_AddsDatadogAttribute()
    {
        // Arrange
        var request = new PublishRequest();
        var proxy = new PublishRequestProxy(request);

        // Act
        ContextPropagation.InjectHeadersIntoMessage<PublishRequest>(proxy, _spanContext);

        // Now, attempt to extract the SpanContext from the message
        var getter = new MemoryStreamCarrierGetter();
        var extracted = DatadogContextPropagator.Instance.TryExtract(proxy, getter, out var extractedSpanContext);

        // Assert
        extracted.Should().BeTrue();
        extractedSpanContext.Should().NotBeNull();
        extractedSpanContext.TraceId.Should().Be(_spanContext.TraceId);
        extractedSpanContext.SpanId.Should().Be(_spanContext.SpanId);
        proxy.CloseMemoryStream();
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
