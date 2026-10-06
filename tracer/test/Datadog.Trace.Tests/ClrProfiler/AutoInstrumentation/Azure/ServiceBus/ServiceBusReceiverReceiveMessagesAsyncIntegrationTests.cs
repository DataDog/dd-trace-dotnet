// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    public class ServiceBusReceiverReceiveMessagesAsyncIntegrationTests
    {
        // Preserve reinjection for manual/non-processor receives in every hosting environment
        // and for the isolated Functions host handoff. Other processor receives keep the producer context.
        [Theory]
        // isProcessorReceive, isIsolatedFunctionHostProcess, expected
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ShouldReinjectContext_ReturnsExpected(bool isProcessorReceive, bool isIsolatedFunctionHostProcess, bool expected)
        {
            ServiceBusReceiverReceiveMessagesAsyncIntegration
                .ShouldReinjectContext(isProcessorReceive, isIsolatedFunctionHostProcess)
                .Should().Be(expected);
        }
    }
}
