// <copyright file="EventHubProducerClientSendEnumerableAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation for IEnumerable&lt;EventData&gt;
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]", ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventHubProducerClientSendEnumerableAsyncIntegration
    {
        private const string OperationName = "azure_eventhubs.send";
        private const string MessagingType = "eventhubs";
        private const int DefaultEventHubsPort = 5671;
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubProducerClientSendEnumerableAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEventDataEnumerable>(
            TTarget instance,
            TEventDataEnumerable eventBatch,
            CancellationToken cancellationToken)
            where TTarget : IEventHubProducerClient, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            Scope? scope = null;

            try
            {
                Log.Debug(LogPrefix + "Starting enumerable send operation for EventHub: {EventHub}", instance.EventHubName);

                var tags = Tracer.Instance.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Producer);
                tags.MessagingDestinationName = instance.EventHubName;
                tags.MessagingOperation = "send";

                scope = Tracer.Instance.StartActiveInternal(OperationName, tags: tags);
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

                // Count events in the enumerable for metrics
                // Note: Context injection is handled automatically by InstrumentMessageIntegration
                // which instruments Azure.Core.Shared.MessagingClientDiagnostics.InstrumentMessage
                if (eventBatch != null)
                {
                    try
                    {
                        var events = (eventBatch as IEnumerable<object>)?.ToList();
                        if (events != null && events.Count > 0)
                        {
                            span.SetMetric("eventhubs.batch.event_count", events.Count);
                            Log.Debug(LogPrefix + "Sending {0} events", (object)events.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, LogPrefix + "Failed to count events in SendAsync enumerable");
                    }
                }

                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating producer span for enumerable send");
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
                    Log.Warning(LogPrefix + "Enumerable send operation failed with exception: {0}", exception.Message);
                }
                else
                {
                    Log.Debug(LogPrefix + "Enumerable send operation completed successfully");
                }

                scope.Dispose();
            }

            return returnValue;
        }
    }
}
