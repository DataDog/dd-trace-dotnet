// <copyright file="AzureServiceBusActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// This Activity handler captures the "Message" Activity objects, whose span context
    /// is injected into the AzureServiceBus message's properties.
    /// </summary>
    internal class AzureServiceBusActivityHandler : IActivityHandler
    {
        public bool ShouldListenTo(string sourceName, string? version)
            => sourceName.StartsWith("Azure.Messaging.ServiceBus");

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            var tags = Tracer.Instance.CurrentTraceSettings.Schema.Client.CreateAzureServiceBusTags();
            ActivityHandlerCommon.ActivityStarted(sourceName, activity, tags: tags, out var activityMapping);
            // TODO: Update the Activity's traceparent and/or tracestate strings here
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
            => ActivityHandlerCommon.ActivityStopped(sourceName, activity);
    }
}
