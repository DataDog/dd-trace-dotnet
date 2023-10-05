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
        [Fact]
        public void OperationName_ShouldBe_HttpServerRequest()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal("http.server.request", span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_HttpClientRequest()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal("http.client.request", span.OperationName);
        }

        [Theory]
        [InlineData("mongodb", "delete")]
        public void OperationName_ShouldBe_DbSystem_Dot_DbOperation(string dbSystem, string dbOperation)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "db.system", dbSystem },
                { "db.operation", dbOperation },
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{dbSystem}.{dbOperation}";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("mongodb")]
        public void OperationName_ShouldBe_DbSystem_Dot_Query(string dbSystem)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "db.system", dbSystem }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{dbSystem}.query";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("kafka", "receive", (int)ActivityKind.Client)]
        [InlineData("kafka", "receive", (int)ActivityKind.Consumer)]
        [InlineData("kafka", "publish", (int)ActivityKind.Producer)]
        public void OperationName_ShouldBe_MessagingSystem_Dot_MessagingOperation(string messagingSystem, string messagingOperation, int kind)
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

        [Fact]
        public void OperationName_ShouldBe_Aws()
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

            var expected = $"aws";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("S3")]
        [InlineData("SNS")]
        public void OperationName_ShouldBe_Aws_Dot_RpcService(string rpcService)
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

            var expected = $"aws.{rpcService.ToLower()}";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("grpc", (int)ActivityKind.Client)]
        [InlineData("grpc", (int)ActivityKind.Server)]
        [InlineData("dotnet_wcf", (int)ActivityKind.Client)]
        [InlineData("dotnet_wcf", (int)ActivityKind.Server)]
        public void OperationName_ShouldBe_RpcSystem_Dot_SpanKind_Dot_Request(string rpcSystem,  int kind)
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
        [InlineData("query")]
        [InlineData("mutation")]
        public void OperationName_ShouldBe_GraphQl_Dot_OperationType(string operationType)
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

            var expected = $"graphql.{operationType}";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("amqp")]
        [InlineData("mqtt")]
        public void OperationName_ShouldBe_NetworkProtocolName_Dot_Server_Dot_Request(string protocol)
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
        public void OperationName_ShouldBe_Unknown_Dot_Server_Dot_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"unknown.server.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("amqp")]
        [InlineData("mqtt")]
        public void OperationName_ShouldBe_NetworkProtocolName_Dot_Client_Dot_Request(string protocol)
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
        public void OperationName_ShouldBe_Unknown_Dot_Client_Dot_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"unknown.client.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Unknown_Dot_Producer_Dot_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Producer);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"unknown.producer.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_Unknown_Dot_Consumer_Dot_Request()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Consumer);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"unknown.consumer.request";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("datasource")]
        [InlineData("http")]
        [InlineData("pubsub")]
        [InlineData("timer")]
        [InlineData("other")]
        public void OperationName_ShouldBe_FaasTrigger_Dot_Trigger(string trigger)
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

            var expected = $"{trigger}.trigger";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("alibaba_cloud")]
        [InlineData("aws")]
        [InlineData("azure")]
        [InlineData("gcp")]
        [InlineData("tencent_cloud")]
        public void OperationName_ShouldBe_FaasInvokedProvider_Dot_Invoke(string provider)
        {
            // https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/faas/
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Client);
            var tagObjects = new Dictionary<string, object>
            {
                { "faas.invoked_provider", provider }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{provider}.invoke";
            Assert.Equal(expected, span.OperationName);
        }

        [Theory]
        [InlineData("Datadog", "Foo()")]
        [InlineData("Datadog.Tracer", "Bar()")]
        public void OperationName_ShouldBe_CodeNamespace_Dot_CodeFunction(string codeNamespace, string functionName)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Internal);
            var tagObjects = new Dictionary<string, object>
            {
                { "code.namespace", codeNamespace },
                { "code.function", functionName }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"{codeNamespace}.{functionName}";
            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void OperationName_ShouldBe_SpanKind()
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Internal);
            var tagObjects = new Dictionary<string, object>
            {
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            var expected = $"internal"; // span.kind.lower() TODO can this be other span.kinds?
            Assert.Equal(expected, span.OperationName);
        }
    }
}
