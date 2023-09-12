// <copyright file="AzureServiceBusActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
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
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && dataStreamsManager.IsEnabled
                && activity.Instance is not null
                && activity.OperationName == "Message"
                && AzureServiceBusCommon.ActiveMessageProperties.Value is IDictionary<string, object> applicationProperties)
            {
                // Adding DSM to the send operation of IReadOnlyCollection<ServiceBusMessage>|ServiceBusMessageBatch - Step Three:
                // If we can retrieve the active message properties object stored in the AsyncLocal field,
                // then we can retrieve the active message object using our mapping. With access to the message
                // object, we can accurately calculate the payload size for the DataStreamsCheckpoint

                string key;
                if (activity is IW3CActivity w3cActivity)
                {
                    key = w3cActivity.TraceId + w3cActivity.SpanId;
                }
                else
                {
                    key = activity.Id;
                }

                if (ActivityHandlerCommon.ActivityMappingById.TryRemove(key, out ActivityMapping activityMapping)
                    && activityMapping.Scope?.Span is Span span)
                {
                    // Copy over the data to our Span object so we can do an efficient tags lookup
                    OtlpHelpers.UpdateSpanFromActivity(activity, span);

                    string namespaceString = span.Tags.GetTag("messaging.destination.name");
                    long? payloadSize = null;
                    if (AzureServiceBusCommon.TryGetMessage(applicationProperties, out var message)
                        && message.TryDuckCast<IServiceBusMessage>(out var serviceBusMessage))
                    {
                        payloadSize = AzureServiceBusCommon.GetMessageSize(serviceBusMessage);
                    }

                    var edgeTags = string.IsNullOrEmpty(namespaceString)
                        ? new[] { "direction:out", "type:servicebus" }
                        : new[] { "direction:out", $"topic:{namespaceString}", "type:servicebus" };

                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Produce,
                        edgeTags,
                        payloadSize ?? 0,
                        0);

                    dataStreamsManager.InjectPathwayContextAsBase64String(span.Context.PathwayContext, new ServiceBusHeadersCollectionAdapter(applicationProperties));

                    // Close the scope and return so we bypass the common code path
                    span.Finish(activity.StartTimeUtc.Add(activity.Duration));
                    activityMapping.Scope.Close();
                    return;
                }
            }

            ActivityHandlerCommon.ActivityStopped(sourceName, activity);
        }
    }
}
