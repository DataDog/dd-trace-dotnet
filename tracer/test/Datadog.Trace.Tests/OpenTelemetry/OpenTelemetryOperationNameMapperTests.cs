// <copyright file="OpenTelemetryOperationNameMapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetryOperationNameMapperTests))]
    public class OpenTelemetryOperationNameMapperTests
    {
        // Note: These test cases were copy/pasted from parametric tests (with some find/replace to make it work here)
        public static TheoryData<string, string, SerializableDictionary> NameData => new()
        {
            // expected_operation_name, span_kind, tags_related_to_operation_name
            { "http.server.request", SpanKinds.Server, new SerializableDictionary { { "http.request.method", "GET" } } },
            { "http.client.request", SpanKinds.Client, new SerializableDictionary { { "http.request.method", "GET" } } },
            { "redis.query", SpanKinds.Client, new SerializableDictionary { { "db.system", "Redis" } } },
            { "kafka.receive", SpanKinds.Client, new SerializableDictionary { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
            { "kafka.receive", SpanKinds.Server, new SerializableDictionary { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
            { "kafka.receive", SpanKinds.Producer, new SerializableDictionary { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
            { "kafka.receive", SpanKinds.Consumer, new SerializableDictionary { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
            { "aws.s3.request", SpanKinds.Client, new SerializableDictionary { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
            { "aws.client.request", SpanKinds.Client, new SerializableDictionary { { "rpc.system", "aws-api" } } },
            { "grpc.client.request", SpanKinds.Client, new SerializableDictionary { { "rpc.system", "GRPC" } } },
            { "grpc.server.request", SpanKinds.Server, new SerializableDictionary { { "rpc.system", "GRPC" } } },
            { "aws.my-function.invoke", SpanKinds.Client, new SerializableDictionary { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
            { "datasource.invoke", SpanKinds.Server, new SerializableDictionary { { "faas.trigger", "Datasource" } } },
            { "graphql.server.request", SpanKinds.Server, new SerializableDictionary { { "graphql.operation.type", "query" } } },
            { "amqp.server.request", SpanKinds.Server, new SerializableDictionary { { "network.protocol.name", "Amqp" } } },
            { "server.request", SpanKinds.Server, new SerializableDictionary() },
            { "amqp.client.request", SpanKinds.Client, new SerializableDictionary() { { "network.protocol.name", "Amqp" } } },
            { "client.request", SpanKinds.Client, new SerializableDictionary() },
            { "internal", SpanKinds.Internal, new SerializableDictionary() },
            { "consumer", SpanKinds.Consumer, new SerializableDictionary() },
            { "producer", SpanKinds.Producer, new SerializableDictionary() },
            { "internal", null, new SerializableDictionary() },
        };

        [Theory]
        [MemberData(nameof(NameData))]
        public void OperationName_ShouldBeSet_BasedOnTags(string expectedOperationName, string expectedActivityKind, SerializableDictionary tags)
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

                Tags = data[2] is null ? new SerializableDictionary() : (SerializableDictionary)data[2];
            }

            public string ExpectedOperationName { get; }

            public string ExpectedActivityKind { get; }

            public SerializableDictionary Tags { get; }
        }
    }
}
