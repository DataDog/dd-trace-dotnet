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
        public static IEnumerable<object[]> NameData =>
            new List<object[]>
            {
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
    }
}
