// <copyright file="EventHubProducerClientSendAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Azure.Messaging.EventHubs.Producer.EventHubProducerClient.SendAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
        MethodName = "SendAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]", "Azure.Messaging.EventHubs.Producer.SendEventOptions", ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventHubProducerClientSendAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.send";
        private const string MessagingType = "eventhubs";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubProducerClientSendAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEvents>(
            TTarget instance,
            TEvents events,
            object? sendEventOptions,
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
                Log.Debug(LogPrefix + "Starting send operation for EventHub: {EventHub}", instance.EventHubName);

                var tags = new EventHubProducerTags
                {
                    EventHubName = instance.EventHubName,
                    Namespace = instance.FullyQualifiedNamespace,
                    Operation = "send"
                };

                scope = Tracer.Instance.StartActiveInternal(OperationName, tags: tags);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = $"send {instance.EventHubName}";

                // Inject context into all events
                if (events is IEnumerable eventsList)
                {
                    int eventCount = 0;
                    foreach (var eventObj in eventsList)
                    {
                        eventCount++;
                        if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
                        {
                            if (eventData.Properties != null)
                            {
                                AzureMessagingCommon.InjectContext(eventData.Properties, scope);
                            }
                            else
                            {
                                Log.Warning(LogPrefix + "EventData.Properties is null, cannot inject trace context for event #{0}", (object)eventCount);
                            }
                        }
                        else
                        {
                            Log.Warning(LogPrefix + "Failed to duck cast event #{0} to IEventData", (object)eventCount);
                        }
                    }

                    if (eventCount > 0)
                    {
                        span.SetTag("messaging.batch.message_count", eventCount.ToString());
                        Log.Debug(LogPrefix + "Injected trace context into {0} events", (object)eventCount);
                    }
                }

                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating or populating scope for EventHub send operation");
            }

            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(
            TTarget instance,
            TReturn returnValue,
            Exception? exception,
            in CallTargetState state)
        {
            var scope = state.Scope;
            if (scope == null)
            {
                return returnValue;
            }

            try
            {
                if (exception != null)
                {
                    scope.Span.SetException(exception);
                    Log.Debug(LogPrefix + "Send operation failed with exception: {ExceptionType}", exception.GetType().Name);
                }
                else
                {
                    Log.Debug(LogPrefix + "Send operation completed successfully");
                }
            }
            finally
            {
                scope.Dispose();
            }

            return returnValue;
        }
    }
}
