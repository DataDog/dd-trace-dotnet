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
using SD = System.Diagnostics;

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
        await AssertGeneratedSpan(handler, "Quartz", nameof(IntegrationId.Quartz), IntegrationId.Quartz, initiallyEnabled);
        await AssertGeneratedSpan(handler, "Quartz", nameof(IntegrationId.Quartz), IntegrationId.Quartz, !initiallyEnabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AzureServiceBusHandlerReevaluatesIntegrationSettingForEachActivity(bool initiallyEnabled)
    {
        var handler = new AzureServiceBusActivityHandler();

        handler.ShouldListenTo("Azure.Messaging.ServiceBus", version: null).Should().BeTrue();
        await AssertGeneratedSpan(handler, "Azure.Messaging.ServiceBus", nameof(IntegrationId.AzureServiceBus), IntegrationId.AzureServiceBus, initiallyEnabled);
        await AssertGeneratedSpan(handler, "Azure.Messaging.ServiceBus", nameof(IntegrationId.AzureServiceBus), IntegrationId.AzureServiceBus, !initiallyEnabled);
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

    private static async Task AssertGeneratedSpan(
        IActivityHandler handler,
        string sourceName,
        string integrationName,
        IntegrationId integrationId,
        bool enabled)
    {
        var settings = TracerSettings.Create(new()
        {
            [string.Format(IntegrationSettings.IntegrationEnabledKey, integrationName.ToUpperInvariant())] = enabled,
        });
        var telemetry = new Mock<ITelemetryController>();
        telemetry.Setup(x => x.DisposeAsync()).Returns(Task.CompletedTask);

        await using var tracer = TracerHelper.Create(settings, agentWriter: Mock.Of<IAgentWriter>(), telemetryController: telemetry.Object);
        TracerRestorerAttribute.SetTracer(tracer);

        var activity = new SD.Activity(sourceName);
        activity.Start();
        var duckActivity = activity.DuckCast<IActivity5>();

        handler.ActivityStarted(sourceName, duckActivity);
        tracer.ActiveScope.Should().NotBeNull();
        var span = (Span)tracer.ActiveScope!.Span;
        if (enabled && integrationId == IntegrationId.AzureServiceBus)
        {
            span.Tags.Should().BeAssignableTo<AzureServiceBusTags>();
        }
        else
        {
            span.Tags.Should().BeOfType<OpenTelemetryTags>();
        }

        handler.ActivityStopped(sourceName, duckActivity);
        activity.Stop();

        var expectedInstrumentationName = enabled
                                              ? integrationId switch
                                              {
                                                  IntegrationId.Quartz => "quartz",
                                                  IntegrationId.AzureServiceBus => nameof(IntegrationId.AzureServiceBus),
                                                  _ => null,
                                              }
                                              : null;
        span.GetTag(Trace.Tags.InstrumentationName).Should().Be(expectedInstrumentationName);

        var expectedIntegrationId = enabled ? integrationId : IntegrationId.OpenTelemetry;
        telemetry.Verify(x => x.IntegrationGeneratedSpan(expectedIntegrationId), Times.Once);
        telemetry.Verify(x => x.IntegrationGeneratedSpan(It.IsAny<IntegrationId>()), Times.Once);
    }
}
