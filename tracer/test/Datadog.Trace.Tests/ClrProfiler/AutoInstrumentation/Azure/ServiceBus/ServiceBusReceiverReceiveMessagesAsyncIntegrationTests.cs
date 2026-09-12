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
        // Pins the reinjection decision for every combination of the three signals available at
        // receive time. Reinjection is only wanted for the Azure Functions Service Bus trigger
        // handoff paths that parent by reading the message context (a manual/non-processor receive,
        // or the isolated Functions host process). A user-created ServiceBusProcessor and the
        // in-process trigger must be excluded, so their producer context survives on the message.
        [Theory]
        // isRunningInAzureFunctions, isProcessorReceive, isIsolatedFunctionHostProcess, expected
        [InlineData(false, false, false, false)] // not Functions: never reinject
        [InlineData(false, false, true,  false)] // not Functions: host flag is meaningless here
        [InlineData(false, true,  false, false)] // not Functions: user processor keeps producer context
        [InlineData(false, true,  true,  false)] // not Functions: host flag is meaningless here
        [InlineData(true,  false, false, true)]  // Functions, manual/non-processor receive -> reinject
        [InlineData(true,  false, true,  true)]  // isolated host, non-processor receive -> reinject
        [InlineData(true,  true,  false, false)] // in-process user processor OR in-process trigger -> do NOT reinject
        [InlineData(true,  true,  true,  true)]  // isolated host trigger handoff -> reinject
        public void ShouldReinjectContext_ReturnsExpected(bool isRunningInAzureFunctions, bool isProcessorReceive, bool isIsolatedFunctionHostProcess, bool expected)
        {
            ServiceBusReceiverReceiveMessagesAsyncIntegration
                .ShouldReinjectContext(isRunningInAzureFunctions, isProcessorReceive, isIsolatedFunctionHostProcess)
                .Should().Be(expected);
        }
    }
}
