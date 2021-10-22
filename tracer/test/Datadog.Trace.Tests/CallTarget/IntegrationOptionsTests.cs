// <copyright file="IntegrationOptionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class IntegrationOptionsTests
    {
        [Fact]
        public void CanGetIntegrationIdFromInstrumentAttribute()
        {
            var integrationType = typeof(HttpClientHandlerIntegration);
            var targetType = typeof(System.Net.Http.HttpClientHandler);

            var info = IntegrationOptions<object, object>.GetIntegrationId(integrationType, targetType);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.HttpMessageHandler);
        }

        [Fact]
        public void CanGetIntegrationIdFromInstrumentAttributeWithMultipleAssemblyNames()
        {
            var integrationType = typeof(XUnitTestInvokerRunAsyncIntegration);
            var targetType = typeof(Xunit.Sdk.TestInvoker<>);

            var info = IntegrationOptions<object, object>.GetIntegrationId(integrationType, targetType);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.XUnit);
        }
    }
}
