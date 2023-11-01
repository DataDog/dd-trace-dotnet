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
        [Theory]
        [InlineData((int)ActivityKind.Server, "http.server.request")]
        [InlineData((int)ActivityKind.Client, "http.client.request")]
        public void OperationName_ShouldBe_Http_SpanKind_Request(int kind, string expected)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns((ActivityKind)kind);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_DbSystem_Query()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "db.system", "mongodb" },
                // db.operation is not required
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "mongodb.query";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("kafka", "receive", (int)ActivityKind.Client)]
        [InlineData("kafka", "receive", (int)ActivityKind.Consumer)]
        [InlineData("kafka", "publish", (int)ActivityKind.Producer)]
        [InlineData("kafka", "receive", (int)ActivityKind.Server)]
        public void OperationName_ShouldBe_MessagingSystem_MessagingOperation(string messagingSystem, string messagingOperation, int kind)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns((ActivityKind)kind);
            var tagObjects = new Dictionary<string, object>
            {
                { "messaging.system", messagingSystem },
                { "messaging.operation", messagingOperation }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{messagingSystem}.{messagingOperation}";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("S3")]
        [InlineData("SNS")]
        public void OperationName_ShouldBe_Aws_RpcService_Request(string rpcService)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "rpc.system", "aws-api" },
                { "rpc.service", rpcService }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"aws.{rpcService.ToLower()}.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Aws_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "rpc.system", "aws-api" } // NOTE: no rpc.service here as it is optional
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "aws.client.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("grpc", (int)ActivityKind.Client)]
        [InlineData("grpc", (int)ActivityKind.Server)]
        [InlineData("dotnet_wcf", (int)ActivityKind.Client)]
        [InlineData("dotnet_wcf", (int)ActivityKind.Server)]
        public void OperationName_ShouldBe_RpcSystem_SpanKind_Request(string rpcSystem, int kind)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns((ActivityKind)kind);
            var tagObjects = new Dictionary<string, object>
            {
                { "rpc.system", rpcSystem }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{rpcSystem}.{((ActivityKind)kind).ToString().ToLower()}.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("alibaba_cloud", "my-function1")]
        [InlineData("aws", "my-function2")]
        [InlineData("azure", "my-function3")]
        [InlineData("gcp", "my-function4")]
        [InlineData("tencent_cloud", "my-function5")]
        public void OperationName_ShouldBe_FaasInvokedProvider_FaasName_Invoke(string provider, string name)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/faas/
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "faas.invoked_provider", provider },
                { "faas.name", provider }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{provider}.${name}.invoke";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("datasource")]
        [InlineData("http")]
        [InlineData("pubsub")]
        [InlineData("timer")]
        [InlineData("other")]
        public void OperationName_ShouldBe_FaasTrigger_Invoke(string trigger)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/faas/
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "faas.trigger", trigger }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{trigger}.invoke";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("query")]
        [InlineData("mutation")]
        public void OperationName_ShouldBe_GraphQl_Server_Request(string operationType)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/instrumentation/graphql/
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "graphql.operation.type", operationType }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "graphql.server.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("amqp")]
        [InlineData("mqtt")]
        public void OperationName_ShouldBe_NetworkProtocolName_Server_Request(string protocol)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/span-general/#network-attributes
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "network.protocol.name", protocol }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{protocol}.server.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Server_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "server.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("amqp")]
        [InlineData("mqtt")]
        public void OperationName_ShouldBe_NetworkProtocolName_Client_Request(string protocol)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/span-general/#network-attributes
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "network.protocol.name", protocol }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{protocol}.client.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Client_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "client.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData((int)ActivityKind.Internal)]
        [InlineData((int)ActivityKind.Consumer)]
        [InlineData((int)ActivityKind.Producer)]
        public void OperationName_ShouldBe_SpanKind(int kind)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns((ActivityKind)kind);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{((ActivityKind)kind).ToString().ToLower()}";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Otel_Unknown()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns((ActivityKind)5555); // just some random value
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = "otel_unknown";
            Assert.Equal(expected, span.OperationName);
        }
    }
}
