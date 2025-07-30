// <copyright file="SqsContextPropagationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SQS;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.SQS;

public class SqsContextPropagationTests
{
    private const string DatadogKey = "_datadog";

    private readonly SpanContext _spanContext;

    public SqsContextPropagationTests()
    {
        const long upper = 1234567890123456789;
        const ulong lower = 9876543210987654321;

        var traceId = new TraceId(upper, lower);
        const ulong spanId = 6766950223540265769;
        _spanContext = new SpanContext(traceId, spanId, 1, "test-sqs", "serverless");
    }

    public static Dictionary<string, MessageAttributeValue> GenerateMessageAttributes(int quantity = 1)
    {
        var max = Math.Min(quantity, 10);
        var messageAttributes = new Dictionary<string, MessageAttributeValue>();
        var messageAttributeValue = new MessageAttributeValue
        {
            DataType = "String",
            StringValue = "Jordan González",
        };
        for (var i = 0; i < max; i++)
        {
            messageAttributes[i.ToString()] = messageAttributeValue;
        }

        return messageAttributes;
    }

    public static IEnumerable<object[]> GetInjectableRequests()
    {
        var requests = new List<object>
        {
            new SendMessageRequest() { MessageBody = "Null Message Attributes", MessageAttributes = null },
            new SendMessageRequest() { MessageBody = "No Message Attributes" },
            new SendMessageRequest() { MessageBody = "Some Message Attributes", MessageAttributes = GenerateMessageAttributes(3) },
            new SendMessageBatchRequestEntry() { MessageBody = "With a Batch entry", MessageAttributes = GenerateMessageAttributes(3) }
        };

        var request = new SendMessageRequest { MessageBody = "DatadogMessageAttribute", MessageAttributes = GenerateMessageAttributes(9) };
        request.MessageAttributes.Add("x-datadog-something", new MessageAttributeValue());
        // we have 10 message attributes (full), but one of them is a "legacy" datadog one that should be replaced, so we'll still inject
        request.MessageAttributes.Count.Should().Be(10);
        requests.Add(request);

        return requests.Select(r => new object[] { r.DuckCast<IContainsMessageAttributes>() });
    }

    [Fact]
    public async Task InjectHeadersIntoMessage_FullMessageAttributes_DoesntAddTraceContext()
    {
        var request = new SendMessageRequest()
        {
            MessageBody = "Too Many Message Attributes",
            MessageAttributes = GenerateMessageAttributes(10)
        };
        var proxy = request.DuckCast<IContainsMessageAttributes>();

        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectHeadersIntoMessage(tracer, proxy, _spanContext, dataStreamsManager: null, CachedMessageHeadersHelper<SendMessageRequest>.Instance);

        proxy.MessageAttributes.Count.Should().Be(10);
        proxy.MessageAttributes.Contains(DatadogKey).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetInjectableRequests))]
    internal async Task AddsTraceContext(IContainsMessageAttributes requestProxy)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        ContextPropagation.InjectHeadersIntoMessage(tracer, requestProxy, _spanContext, dataStreamsManager: null, CachedMessageHeadersHelper<SendMessageRequest>.Instance);

        var messageAttributes = (Dictionary<string, MessageAttributeValue>)requestProxy.MessageAttributes;
        messageAttributes.Count.Should().BeLessOrEqualTo(10);

        var extracted = messageAttributes.TryGetValue(DatadogKey, out var datadogMessageAttribute);
        extracted.Should().BeTrue();

        // Naively deserialize in order to not use tracer extraction logic
        var extractedTraceContext = JsonConvert.DeserializeObject<Dictionary<string, string>>(datadogMessageAttribute.StringValue);
        extractedTraceContext["x-datadog-parent-id"].Should().Be(_spanContext.SpanId.ToString());
        extractedTraceContext["x-datadog-trace-id"].Should().Be(_spanContext.TraceId.ToString());
    }
}
