// <copyright file="IntegrationActivityHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Activity;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class IntegrationActivityHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuartzHandlerReevaluatesIntegrationSettingForEachActivity(bool initiallyEnabled)
    {
        var handler = new QuartzActivityHandler();

        handler.ShouldListenTo("Quartz", version: null).Should().BeTrue();
        await AssertQuartzGeneratedSpan(handler, initiallyEnabled);
        await AssertQuartzGeneratedSpan(handler, !initiallyEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzureServiceBusHandlerReevaluatesIntegrationSettingForEachActivity(bool initiallyEnabled)
    {
        var handler = new AzureServiceBusActivityHandler();

        handler.ShouldListenTo("Azure.Messaging.ServiceBus", version: null).Should().BeTrue();
        await AssertAzureServiceBusGeneratedSpan(handler, initiallyEnabled);
        await AssertAzureServiceBusGeneratedSpan(handler, !initiallyEnabled);
    }

    [Theory]
    [InlineData("Quartz.Job.Execute", true)]
    [InlineData("Quartz.Job.Veto", true)]
    [InlineData("Quartz", false)]
    [InlineData("Quartz.Job.Execute.Unrelated", false)]
    [InlineData("Azure.Messaging.ServiceBus.Send", false)]
    public void QuartzHandlerOnlyMatchesKnownLegacyOperationNames(string operationName, bool expected)
    {
        new QuartzActivityHandler().ShouldListenToOperationName(operationName).Should().Be(expected);
    }

    // Creates a System.Diagnostics.Activity using the shape available on the current runtime. Quartz metadata
    // requires IActivity5, but older runtimes still create and activate the span through ActivityHandlerCommon.
    private static async Task AssertQuartzGeneratedSpan(QuartzActivityHandler handler, bool enabled)
    {
        var settings = TracerSettings.Create(new()
        {
            [string.Format(IntegrationSettings.IntegrationEnabledKey, nameof(IntegrationId.Quartz).ToUpperInvariant())] = enabled,
        });
        var telemetry = new Mock<ITelemetryController>();
        telemetry.Setup(x => x.DisposeAsync()).Returns(Task.CompletedTask);

        await using var tracer = TracerHelper.Create(settings, agentWriter: Mock.Of<IAgentWriter>(), telemetryController: telemetry.Object);
        TracerRestorerAttribute.SetTracer(tracer);

        var activity = new System.Diagnostics.Activity("Quartz");
        activity.Start();
        var supportsQuartzMetadata = activity.TryDuckCast<IActivity5>(out var activity5);
        IActivity duckActivity = supportsQuartzMetadata ? activity5 : activity.DuckCast<IActivity>();

        handler.ActivityStarted("Quartz", duckActivity);
        var span = (Span)tracer.ActiveScope!.Span;

        handler.ActivityStopped("Quartz", duckActivity);
        activity.Stop();

        span.GetTag(Trace.Tags.InstrumentationName).Should().Be(enabled && supportsQuartzMetadata ? "quartz" : null);
        AssertGeneratedSpanTelemetry(telemetry, IntegrationId.Quartz, enabled);
    }

    // Creates a System.Diagnostics.Activity and passes it through the Azure Service Bus handler, which
    // creates and activates the Datadog span through ActivityHandlerCommon before the test verifies the result.
    private static async Task AssertAzureServiceBusGeneratedSpan(AzureServiceBusActivityHandler handler, bool enabled)
    {
        var settings = TracerSettings.Create(new()
        {
            [string.Format(IntegrationSettings.IntegrationEnabledKey, nameof(IntegrationId.AzureServiceBus).ToUpperInvariant())] = enabled,
        });
        var telemetry = new Mock<ITelemetryController>();
        telemetry.Setup(x => x.DisposeAsync()).Returns(Task.CompletedTask);

        await using var tracer = TracerHelper.Create(settings, agentWriter: Mock.Of<IAgentWriter>(), telemetryController: telemetry.Object);
        TracerRestorerAttribute.SetTracer(tracer);

        var activity = new System.Diagnostics.Activity("Azure.Messaging.ServiceBus");
        activity.Start();
        var duckActivity = activity.DuckCast<IActivity>();

        handler.ActivityStarted("Azure.Messaging.ServiceBus", duckActivity);
        var span = (Span)tracer.ActiveScope!.Span;
        if (enabled)
        {
            span.Tags.Should().BeAssignableTo<AzureServiceBusTags>();
        }
        else
        {
            span.Tags.Should().BeOfType<OpenTelemetryTags>();
        }

        activity.Stop();
        handler.ActivityStopped("Azure.Messaging.ServiceBus", duckActivity);

        AssertGeneratedSpanTelemetry(telemetry, IntegrationId.AzureServiceBus, enabled);
    }

    private static void AssertGeneratedSpanTelemetry(Mock<ITelemetryController> telemetry, IntegrationId integrationId, bool enabled)
    {
        var expectedIntegrationId = enabled ? integrationId : IntegrationId.OpenTelemetry;
        telemetry.Verify(x => x.IntegrationGeneratedSpan(expectedIntegrationId), Times.Once);
        telemetry.Verify(x => x.IntegrationGeneratedSpan(It.IsAny<IntegrationId>()), Times.Once);
    }
}
