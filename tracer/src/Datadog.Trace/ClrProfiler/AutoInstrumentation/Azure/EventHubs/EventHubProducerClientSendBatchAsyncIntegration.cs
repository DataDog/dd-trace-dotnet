// <copyright file="EventHubProducerClientSendBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation for EventDataBatch
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.Producer.EventDataBatch", ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventHubProducerClientSendBatchAsyncIntegration
    {
        private const string OperationName = "azure_eventhubs.send";
        private const string MessagingType = "eventhubs";
        private const int DefaultEventHubsPort = 5671;
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubProducerClientSendBatchAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEventBatch>(
            TTarget instance,
            TEventBatch eventBatch,
            CancellationToken cancellationToken)
            where TTarget : IEventHubProducerClient, IDuckType
            where TEventBatch : IEventDataBatch, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            Scope? scope = null;

            try
            {
                Log.Debug(LogPrefix + "Starting batch send operation for EventHub: {EventHub}", instance.EventHubName);

                var tags = Tracer.Instance.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Producer);
                tags.MessagingDestinationName = instance.EventHubName;
                tags.MessagingOperation = "send";

                var spanContexts = EventHubsCommon.RetrieveAndClearSpanContexts(eventBatch?.Instance);
                var spanLinks = spanContexts?.Select(ctx => new SpanLink(ctx));
                scope = Tracer.Instance.StartActiveInternal(OperationName, tags: tags, links: spanLinks);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = instance.EventHubName;

                // Set network destination tags
                var endpoint = instance.Connection?.ServiceEndpoint;
                if (endpoint != null)
                {
                    tags.NetworkDestinationName = endpoint.Host;
                    // https://learn.microsoft.com/en-us/dotnet/api/system.uri.port?view=net-8.0#remarks
                    var port = endpoint.Port == -1 ? DefaultEventHubsPort : endpoint.Port;
                    tags.NetworkDestinationPort = port.ToString();
                }

                // Log batch information
                if (eventBatch != null && eventBatch.Instance != null)
                {
                    var count = eventBatch.Count;
                    span.SetMetric("eventhubs.batch.event_count", count);
                    span.SetMetric("eventhubs.batch.size_bytes", eventBatch.SizeInBytes);
                    Log.Debug(LogPrefix + "Sending batch with {0} events, size: {1} bytes", count, eventBatch.SizeInBytes);

                    // Note: We cannot inject context into EventDataBatch as the events are already serialized
                    // This is a limitation of batch sending - context must be injected before adding to batch
                }

                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating producer span for batch send");
                scope?.Dispose();
                return CallTargetState.GetDefault();
            }
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            var scope = state.Scope;
            if (scope != null)
            {
                if (exception != null)
                {
                    scope.Span.SetException(exception);
                    Log.Warning(LogPrefix + "Batch send operation failed with exception: {0}", exception.Message);
                }
                else
                {
                    Log.Debug(LogPrefix + "Batch send operation completed successfully");
                }

                scope.Dispose();
            }

            return returnValue;
        }
    }
}
