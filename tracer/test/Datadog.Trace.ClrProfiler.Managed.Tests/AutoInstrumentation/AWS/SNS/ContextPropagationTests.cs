// <copyright file="ContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.SimpleNotificationService.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.SNS;

public class ContextPropagationTests
{
    private const string DatadogKey = "_datadog";
    private const string TopicArn = "arn:aws:sns:region:000000000:my-topic-name";

    private readonly SpanContext _spanContext;

    public ContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        const ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 1, "test-sns", "serverless");
    }

    public static PublishBatchRequest GeneratePublishBatchRequest(List<PublishBatchRequestEntry> entries)
    {
        var request = new PublishBatchRequest
        {
            TopicArn = TopicArn,
            PublishBatchRequestEntries = entries
        };

        return request;
    }

    public static Dictionary<string, MessageAttributeValue> GenerateMessageAttributes(int quantity = 1)
    {
        var max = Math.Min(quantity, 10);
        var messageAttributes = new Dictionary<string, MessageAttributeValue>();
        var messageAttributeValue = new MessageAttributeValue()
        {
            DataType = "Binary",
            BinaryValue = new MemoryStream(Encoding.UTF8.GetBytes("Jordan González")),
        };
        for (var i = 0; i < max; i++)
        {
            messageAttributes[i.ToString()] = messageAttributeValue;
        }

        return messageAttributes;
    }

    [Fact]
    public void InjectHeadersIntoBatch_EmptyMessageAttributes_AddsTraceContext()
    {
        var request = GeneratePublishBatchRequest(
            new()
            {
                new()
                {
                    Message = "Message1",
                    Id = "1",
                },
                new()
                {
                    Message = "Message2",
                    Id = "2",
                }
            });

        var proxy = request.DuckCast<IPublishBatchRequest>();

        ContextPropagation.InjectHeadersIntoBatch<PublishBatchRequest, IPublishBatchRequest>(proxy, _spanContext);

        for (int i = 0; i < proxy.PublishBatchRequestEntries.Count; i++)
        {
            // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
            var message = (PublishBatchRequestEntry)proxy.PublishBatchRequestEntries[i];

            // Naively deserialize in order to not use tracer extraction logic
            var messageAttributes = message.MessageAttributes;
            messageAttributes.Count.Should().Be(1);

            var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
            extracted.Should().BeTrue();

            // Cast into a Dictionary<string, string> so we can read it properly
            var jsonString = Encoding.UTF8.GetString(datadogMessageAttribute.BinaryValue.ToArray());
            var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

            extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
            extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        }
    }

    [Fact]
    public void InjectHeadersIntoBatch_WithMessageAttributes_AddsTraceContext()
    {
        var request = GeneratePublishBatchRequest(
            new()
            {
                new()
                {
                    Message = "Message1",
                    Id = "1",
                    MessageAttributes = GenerateMessageAttributes(2)
                },
                new()
                {
                    Message = "Message2",
                    Id = "2",
                    MessageAttributes = GenerateMessageAttributes(2)
                }
            });

        var proxy = request.DuckCast<IPublishBatchRequest>();

        ContextPropagation.InjectHeadersIntoBatch<PublishBatchRequest, IPublishBatchRequest>(proxy, _spanContext);

        for (int i = 0; i < proxy.PublishBatchRequestEntries.Count; i++)
        {
            // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
            var message = (PublishBatchRequestEntry)proxy.PublishBatchRequestEntries[i];

            // Naively deserialize in order to not use tracer extraction logic
            var messageAttributes = message.MessageAttributes;
            messageAttributes.Count.Should().Be(3);

            var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
            extracted.Should().BeTrue();

            // Cast into a Dictionary<string, string> so we can read it properly
            var jsonString = Encoding.UTF8.GetString(datadogMessageAttribute.BinaryValue.ToArray());
            var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

            extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
            extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
        }
    }

    [Fact]
    public void InjectHeadersIntoBatch_FullMessageAttributes_SkipsAddingTraceContext()
    {
        var request = GeneratePublishBatchRequest(
            new()
            {
                new()
                {
                    Message = "Message1",
                    Id = "1",
                    MessageAttributes = GenerateMessageAttributes(10)
                },
                new()
                {
                    Message = "Message2",
                    Id = "2",
                    MessageAttributes = GenerateMessageAttributes(10)
                }
            });

        var proxy = request.DuckCast<IPublishBatchRequest>();
        ContextPropagation.InjectHeadersIntoBatch<PublishBatchRequest, IPublishBatchRequest>(proxy, _spanContext);

        for (int i = 0; i < proxy.PublishBatchRequestEntries.Count; i++)
        {
            // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
            var message = (PublishBatchRequestEntry)proxy.PublishBatchRequestEntries[i];

            // Naively deserialize in order to not use tracer extraction logic
            var messageAttributes = message.MessageAttributes;
            messageAttributes.Count.Should().Be(10);

            var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
            extracted.Should().BeFalse();

            datadogMessageAttribute.Should().Be(null);
        }
    }

    [Fact]
    public void InjectHeadersIntoMessage_EmptyMessageAttributes_AddsTraceContext()
    {
        var request = new PublishRequest()
        {
            TopicArn = TopicArn,
            Message = "No Message Attributes!"
        };

        var proxy = request.DuckCast<IPublishRequest>();

        ContextPropagation.InjectHeadersIntoMessage<PublishRequest, IPublishRequest>(proxy, _spanContext);

        // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
        var messageAttributes = (Dictionary<string, MessageAttributeValue>)proxy.MessageAttributes;
        messageAttributes.Count.Should().Be(1);

        // Naively deserialize in order to not use tracer extraction logic
        var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can read it properly
        var jsonString = Encoding.UTF8.GetString(datadogMessageAttribute.BinaryValue.ToArray());
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
    }

    [Fact]
    public void InjectHeadersIntoMessage_WithMessageAttributes_AddsTraceContext()
    {
        var request = new PublishRequest()
        {
            TopicArn = TopicArn,
            Message = "Some Message Attributes!",
            MessageAttributes = GenerateMessageAttributes(3)
        };

        var proxy = request.DuckCast<IPublishRequest>();

        ContextPropagation.InjectHeadersIntoMessage<PublishRequest, IPublishRequest>(proxy, _spanContext);

        // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
        var messageAttributes = (Dictionary<string, MessageAttributeValue>)proxy.MessageAttributes;
        messageAttributes.Count.Should().Be(4);

        // Naively deserialize in order to not use tracer extraction logic
        var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
        extracted.Should().BeTrue();

        // Cast into a Dictionary<string, string> so we can read it properly
        var jsonString = Encoding.UTF8.GetString(datadogMessageAttribute.BinaryValue.ToArray());
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

        extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
    }

    [Fact]
    public void InjectHeadersIntoMessage_FullMessageAttributes_AddsTraceContext()
    {
        var request = new PublishRequest()
        {
            TopicArn = TopicArn,
            Message = "Some Message Attributes!",
            MessageAttributes = GenerateMessageAttributes(10)
        };

        var proxy = request.DuckCast<IPublishRequest>();

        ContextPropagation.InjectHeadersIntoMessage<PublishRequest, IPublishRequest>(proxy, _spanContext);

        // Hard-casting into PublishBatchRequestEntry because trace context assertion is needed
        var messageAttributes = (Dictionary<string, MessageAttributeValue>)proxy.MessageAttributes;
        messageAttributes.Count.Should().Be(10);

        // Naively deserialize in order to not use tracer extraction logic
        var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
        extracted.Should().BeFalse();

        datadogMessageAttribute.Should().Be(null);
    }
}
