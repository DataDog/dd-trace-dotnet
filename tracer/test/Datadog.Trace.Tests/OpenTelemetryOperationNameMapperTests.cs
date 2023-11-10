// <copyright file="OpenTelemetryOperationNameMapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Tagging;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetryOperationNameMapperTests))]
    public class OpenTelemetryOperationNameMapperTests
    {
        // Note: These test cases were copy/pasted from parametric tests (with some find/replace to make it work here)
        public static IEnumerable<object[]> NameData =>
            new List<object[]>
            {
                // expected_operation_name, span_kind, tags_related_to_operation_name
                new object[] { "http.server.request", SpanKinds.Server, new Dictionary<string, string>() { { "http.request.method", "GET" } } },
                new object[] { "http.client.request", SpanKinds.Client, new Dictionary<string, string>() { { "http.request.method", "GET" } } },
                new object[] { "redis.query", SpanKinds.Client, new Dictionary<string, string>() { { "db.system", "Redis" } } },
                new object[] { "kafka.receive", SpanKinds.Client, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKinds.Server, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKinds.Producer, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKinds.Consumer, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "aws.s3.request", SpanKinds.Client, new Dictionary<string, string>() { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
                new object[] { "aws.client.request", SpanKinds.Client, new Dictionary<string, string>() { { "rpc.system", "aws-api" } } },
                new object[] { "grpc.client.request", SpanKinds.Client, new Dictionary<string, string>() { { "rpc.system", "GRPC" } } },
                new object[] { "grpc.server.request", SpanKinds.Server, new Dictionary<string, string>() { { "rpc.system", "GRPC" } } },
                new object[] { "aws.my-function.invoke", SpanKinds.Client, new Dictionary<string, string>() { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
                new object[] { "datasource.invoke", SpanKinds.Server, new Dictionary<string, string>() { { "faas.trigger", "Datasource" } } },
                new object[] { "graphql.server.request", SpanKinds.Server, new Dictionary<string, string>() { { "graphql.operation.type", "query" } } },
                new object[] { "amqp.server.request", SpanKinds.Server, new Dictionary<string, string>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "server.request", SpanKinds.Server, new Dictionary<string, string>() },
                new object[] { "amqp.client.request", SpanKinds.Client, new Dictionary<string, string>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "client.request", SpanKinds.Client, new Dictionary<string, string>() },
                new object[] { "internal", SpanKinds.Internal, new Dictionary<string, string>() },
                new object[] { "consumer", SpanKinds.Consumer, new Dictionary<string, string>() },
                new object[] { "producer", SpanKinds.Producer, new Dictionary<string, string>() },
                new object[] { "otel_unknown", null, new Dictionary<string, string>() },
            };

        [Theory]
        [MemberData(nameof(NameData))]
        public void OperationName_ShouldBeSet_BasedOnTags(string expectedOperationName, string expectedActivityKind, Dictionary<string, string> tags)
        {
            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow, new OpenTelemetryTags());

            if (!string.IsNullOrEmpty(expectedActivityKind))
            {
                span.Tags.SetTag("span.kind", expectedActivityKind);
            }

            if (tags is not null)
            {
                foreach (var tag in (tags))
                {
                    span.Tags.SetTag(tag.Key, tag.Value.ToString());
                }
            }

            OperationNameMapper.MapToOperationName(span);

            Assert.Equal(expectedOperationName, span.OperationName);
        }

        [Theory]
        [InlineData(SpanKinds.Server)]
        [InlineData(SpanKinds.Client)]
        [InlineData(SpanKinds.Consumer)]
        [InlineData(SpanKinds.Producer)]
        [InlineData(SpanKinds.Internal)]
        public void OperationName_ShouldFollow_PriorityOrder(string expectedActivityKind)
        {
            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow, new OpenTelemetryTags());
            var order = new Queue<OperationNameData>();
            span.Tags.SetTag("span.kind", expectedActivityKind);
            foreach (var data in NameData)
            {
                var operationNameData = new OperationNameData(data);
                if (operationNameData.ExpectedActivityKind != expectedActivityKind)
                {
                    continue;
                }

                foreach (var tag in operationNameData.Tags)
                {
                    span.Tags.SetTag(tag.Key, tag.Value);
                }

                order.Enqueue(operationNameData);
            }

            // map the name, assert it is the expected  operation name, then remove necessary tags, repeat
            while (order.Count > 0)
            {
                var expected = order.Dequeue();
                OperationNameMapper.MapToOperationName(span);

                Assert.Equal(expected.ExpectedOperationName, span.OperationName);

                // can't reach the RemoveTag with SetTag with a null value for types that define tags so need to recreate entirely
                span.Tags = new OpenTelemetryTags();
                foreach (var remainingData in order)
                {
                    foreach (var tag in remainingData.Tags)
                    {
                        // hacky, if we already have set the tag, skip
                        // this will be skipping the lower priority tag
                        if (span.Tags.GetTag(tag.Key) is not null)
                        {
                            continue;
                        }

                        span.Tags.SetTag(tag.Key, tag.Value);
                    }
                }

                span.Tags.SetTag("span.kind", expectedActivityKind);

                // must clear OperationName as this method will not override it if there is a value
                // this is because prior to this we may see an `operation.name` tag and use that value
                span.OperationName = null;
            }
        }

        private class OperationNameData
        {
            public OperationNameData(object[] data)
            {
                if (data.Length != 3)
                {
                    throw new ArgumentException("Invalid data for operation name", nameof(data));
                }

                ExpectedOperationName = data[0].ToString();

                ExpectedActivityKind = data[1]?.ToString() ?? string.Empty;

                Tags = data[2] is null ? new Dictionary<string, string>() : (Dictionary<string, string>)data[2];
            }

            public string ExpectedOperationName { get; }

            public string ExpectedActivityKind { get; }

            public Dictionary<string, string> Tags { get; }
        }
    }
}
