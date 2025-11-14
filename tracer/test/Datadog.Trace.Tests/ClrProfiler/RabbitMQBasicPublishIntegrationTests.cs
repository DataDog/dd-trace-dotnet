// <copyright file="RabbitMQBasicPublishIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler
{
    public class RabbitMQBasicPublishIntegrationTests
    {
        [Fact]
        public async Task OnMethodBegin_WithReadOnlyHeaders_DoesNotThrow()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            await using var tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            var readOnlyHeaders = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
            {
                { "existing-key", "existing-value" }
            });

            var basicProperties = new TestBasicProperties
            {
                Headers = readOnlyHeaders
            };

            var target = new TestModelBase();

            Exception caughtException = null;

            try
            {
                var state = BasicPublishIntegration.OnMethodBegin(
                    target,
                    "test-exchange",
                    "test-routing-key",
                    false,
                    basicProperties,
                    new TestBody());

                state.Scope?.Dispose();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            caughtException.Should().BeNull("the integration should handle read-only headers gracefully");
            basicProperties.Headers.Should().NotBeNull();
            basicProperties.Headers.IsReadOnly.Should().BeFalse("headers should be replaced with a mutable dictionary");
            basicProperties.Headers.Should().ContainKey("existing-key");
            basicProperties.Headers.Should().ContainKey("x-datadog-trace-id");
        }

        [Fact]
        public async Task OnMethodBegin_WithNullHeaders_CreatesNewDictionary()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            await using var tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            var basicProperties = new TestBasicProperties
            {
                Headers = null
            };

            var target = new TestModelBase();

            var state = BasicPublishIntegration.OnMethodBegin(
                target,
                "test-exchange",
                "test-routing-key",
                false,
                basicProperties,
                new TestBody());

            state.Scope?.Dispose();

            basicProperties.Headers.Should().NotBeNull();
            basicProperties.Headers.Should().ContainKey("x-datadog-trace-id");
        }

        [Fact]
        public async Task OnMethodBegin_WithWritableHeaders_AddsHeaders()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();
            await using var tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            var writableHeaders = new Dictionary<string, object>
            {
                { "existing-key", "existing-value" }
            };

            var basicProperties = new TestBasicProperties
            {
                Headers = writableHeaders
            };

            var target = new TestModelBase();

            var state = BasicPublishIntegration.OnMethodBegin(
                target,
                "test-exchange",
                "test-routing-key",
                false,
                basicProperties,
                new TestBody());

            state.Scope?.Dispose();

            basicProperties.Headers.Should().NotBeNull();
            basicProperties.Headers.Should().BeSameAs(writableHeaders, "writable headers should not be replaced");
            basicProperties.Headers.Should().ContainKey("existing-key");
            basicProperties.Headers.Should().ContainKey("x-datadog-trace-id");
        }

        private class TestBasicProperties : IBasicProperties
        {
            public IDictionary<string, object> Headers { get; set; }

            public object Instance => this;

            public Type Type => typeof(TestBasicProperties);

            public byte DeliveryMode => 1;

            public AmqpTimestamp Timestamp => default;

            public bool IsDeliveryModePresent() => true;

            public ref TReturn GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }
        }

        private class TestBody : IBody
        {
            public object Instance => Array.Empty<byte>();

            public Type Type => typeof(byte[]);

            public int Length => 0;

            public ref TReturn GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }
        }

        private class TestModelBase : IModelBase
        {
            public ISession? Session => new ISession
            {
                Connection = new IConnection
                {
                    Endpoint = new IAmqpTcpEndpoint
                    {
                        HostName = "localhost"
                    }
                }
            };

            public object Instance => this;

            public Type Type => typeof(TestModelBase);

            public ref TReturn GetInternalDuckTypedInstance<TReturn>()
            {
                throw new NotImplementedException();
            }
        }
    }
}
