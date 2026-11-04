// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    public class ServiceBusReceiverReceiveMessagesAsyncIntegrationTests
    {
        // Preserve reinjection for manual/non-processor receives in every hosting environment
        // and for the isolated Functions host handoff. Only continue may preserve the producer context.
        [Theory]
        // isProcessorReceive, isIsolatedFunctionHostProcess, extractionBehavior, expected
        [InlineData(false, false, (int)ExtractBehavior.Continue, true)]
        [InlineData(false, true, (int)ExtractBehavior.Continue, true)]
        [InlineData(true, false, (int)ExtractBehavior.Continue, false)]
        [InlineData(true, true, (int)ExtractBehavior.Continue, true)]
        [InlineData(false, false, (int)ExtractBehavior.Restart, true)]
        [InlineData(false, true, (int)ExtractBehavior.Restart, true)]
        [InlineData(true, false, (int)ExtractBehavior.Restart, true)]
        [InlineData(true, true, (int)ExtractBehavior.Restart, true)]
        [InlineData(false, false, (int)ExtractBehavior.Ignore, true)]
        [InlineData(false, true, (int)ExtractBehavior.Ignore, true)]
        [InlineData(true, false, (int)ExtractBehavior.Ignore, true)]
        [InlineData(true, true, (int)ExtractBehavior.Ignore, true)]
        public void ShouldReinjectContext_ReturnsExpected(bool isProcessorReceive, bool isIsolatedFunctionHostProcess, int extractionBehavior, bool expected)
        {
            ServiceBusReceiverReceiveMessagesAsyncIntegration
                .ShouldReinjectContext(isProcessorReceive, isIsolatedFunctionHostProcess, (ExtractBehavior)extractionBehavior)
                .Should().Be(expected);
        }
    }
}
