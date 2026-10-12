// <copyright file="AzureServiceBusActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Handlers
{
    internal sealed class AzureServiceBusActivityHandler : IActivityHandler
    {
        private static readonly DefaultActivityHandler DefaultHandler = new();

        public bool ShouldListenTo(string sourceName, string? version)
            => sourceName.StartsWith("Azure.Messaging.ServiceBus");

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                DefaultHandler.ActivityStarted(sourceName, activity);
                return;
            }

            var tags = Tracer.Instance.CurrentTraceSettings.Schema.Client.CreateAzureServiceBusTags();
            ActivityHandlerCommon.ActivityStarted(IntegrationId.AzureServiceBus, sourceName, activity, tags: tags, out _);
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            ActivityHandlerCommon.ActivityStopped(sourceName, activity);
        }
    }
}
