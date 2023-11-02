// <copyright file="OpenTelemetryOperationNameMapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetryOperationNameMapperTests))]
    public class OpenTelemetryOperationNameMapperTests
    {
        public static IEnumerable<object[]> Activity5Data =>
            new List<object[]>
            {
                new object[] { "http.server.request", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "http.client.request", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "redis.query", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "db.system", "Redis" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Producer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Consumer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "aws.s3.request", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
                new object[] { "aws.client.request", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" } } },
                new object[] { "grpc.client.request", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "grpc.server.request", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "aws.my-function.invoke", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
                new object[] { "datasource.invoke", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "faas.trigger", "Datasource" } } },
                new object[] { "graphql.server.request", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "graphql.operation.type", "query" } } },
                new object[] { "amqp.server.request", (int?)ActivityKind.Server, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "server.request", (int?)ActivityKind.Server, new Dictionary<string, object>() },
                new object[] { "amqp.client.request", (int?)ActivityKind.Client, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "client.request", (int?)ActivityKind.Client, new Dictionary<string, object>() },
                new object[] { "internal", (int?)ActivityKind.Internal, new Dictionary<string, object>() },
                new object[] { "consumer", (int?)ActivityKind.Consumer, new Dictionary<string, object>() },
                new object[] { "producer", (int?)ActivityKind.Producer, new Dictionary<string, object>() },
                // new object[] { "otel_unknown", null, new Dictionary<string, object>() }, // always should have a span kind for Activity5+
            };

        public static IEnumerable<object[]> W3CActivityData =>
            new List<object[]>
            {
                new object[] { "http.server.request", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "http.request.method", "GET" } } },
                new object[] { "http.client.request", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "http.request.method", "GET" } } },
                new object[] { "redis.query", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "db.system", "Redis" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Producer, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", (int?)ActivityKind.Consumer, new Dictionary<string, string>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "aws.s3.request", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
                new object[] { "aws.client.request", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "rpc.system", "aws-api" } } },
                new object[] { "grpc.client.request", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "rpc.system", "GRPC" } } },
                new object[] { "grpc.server.request", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "rpc.system", "GRPC" } } },
                new object[] { "aws.my-function.invoke", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
                new object[] { "datasource.invoke", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "faas.trigger", "Datasource" } } },
                new object[] { "graphql.server.request", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "graphql.operation.type", "query" } } },
                new object[] { "amqp.server.request", (int?)ActivityKind.Server, new Dictionary<string, string>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "server.request", (int?)ActivityKind.Server, new Dictionary<string, string>() },
                new object[] { "amqp.client.request", (int?)ActivityKind.Client, new Dictionary<string, string>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "client.request", (int?)ActivityKind.Client, new Dictionary<string, string>() },
                new object[] { "internal", (int?)ActivityKind.Internal, new Dictionary<string, string>() },
                new object[] { "consumer", (int?)ActivityKind.Consumer, new Dictionary<string, string>() },
                new object[] { "producer", (int?)ActivityKind.Producer, new Dictionary<string, string>() },
                new object[] { "otel_unknown", null, new Dictionary<string, string>() },
            };

        [Theory]
        [MemberData(nameof(Activity5Data))]
        public void OperationName_ShouldBeSet_BasedOnTags(string expectedOperationName, int? expectedActivityKind, Dictionary<string, object> tags)
        {
            var activityMock = new Mock<IActivity5>();
            if (expectedActivityKind is not null)
            {
                activityMock.Setup(x => x.Kind).Returns((ActivityKind)expectedActivityKind);
            }

            activityMock.Setup(x => x.TagObjects).Returns(tags);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expectedOperationName, span.OperationName);
        }

        [Theory]
        [MemberData(nameof(W3CActivityData))]
        public void OperationName_ShouldBeSet_BasedOnTags_OldActivity(string expectedOperationName, int? expectedActivityKind, Dictionary<string, string> tags)
        {
            var activityMock = new Mock<IW3CActivity>();
            if (expectedActivityKind is not null)
            {
                tags.Add("span.kind", ((ActivityKind)expectedActivityKind).ToString().ToLower()); // there is no ".Kind" property for this IActivity
            }

            activityMock.Setup(x => x.Tags).Returns(tags);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expectedOperationName, span.OperationName);
        }
    }
}
