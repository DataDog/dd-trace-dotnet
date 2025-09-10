// <copyright file="EventHubProducerClientSendBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
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
        private const string OperationName = "azure-eventhubs.send";
        private const string MessagingType = "eventhubs";
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

                // Log batch information and create span links
                if (eventBatch != null && eventBatch.Instance != null)
                {
                    var count = eventBatch.Count;
                    span.SetMetric("eventhubs.batch.event_count", count);
                    span.SetMetric("eventhubs.batch.size_bytes", eventBatch.SizeInBytes);
                    Log.Debug(LogPrefix + "Sending batch with {0} events, size: {1} bytes", count, eventBatch.SizeInBytes);

                    // Create span links from the diagnostic identifiers stored during TryAdd operations
                    try
                    {
                        Log.Debug(LogPrefix + "Attempting to retrieve diagnostic identifiers from batch for span linking");

                        var diagnosticIdentifiers = eventBatch.GetTraceContext();

                        if (diagnosticIdentifiers == null)
                        {
                            Log.Debug(LogPrefix + "No diagnostic identifiers available from batch.GetTraceContext() - returned null");
                        }
                        else if (diagnosticIdentifiers.Count == 0)
                        {
                            Log.Debug(LogPrefix + "Batch diagnostic identifiers collection is empty - no TryAdd operations were traced");
                        }
                        else
                        {
                            Log.Debug(LogPrefix + "Retrieved {0} diagnostic identifiers from batch, creating span links", (object)diagnosticIdentifiers.Count);

                            var successfulLinks = 0;
                            var index = 0;

                            foreach (var traceContext in diagnosticIdentifiers)
                            {
                                index++;

                                if (traceContext?.Instance == null)
                                {
                                    Log.Debug(LogPrefix + "Diagnostic identifier {0}: Instance is null, skipping", (object)index);
                                    continue;
                                }

                                var traceParent = traceContext.Item1;
                                var traceState = traceContext.Item2;

                                Log.Debug(
                                    LogPrefix + "Diagnostic identifier {0}: TraceParent='{1}', TraceState='{2}'",
                                    (object)index,
                                    traceParent ?? "null",
                                    traceState ?? "null");

                                if (string.IsNullOrEmpty(traceParent))
                                {
                                    Log.Debug(LogPrefix + "Diagnostic identifier {0}: TraceParent is null or empty, cannot create span link", (object)index);
                                    continue;
                                }

                                if (!W3CTraceContextPropagator.TryParseTraceParent(traceParent!, out var parsedTraceParent))
                                {
                                    Log.Warning(
                                        LogPrefix + "Diagnostic identifier {0}: Failed to parse W3C traceparent '{1}' - invalid format",
                                        (object)index,
                                        traceParent);
                                    continue;
                                }

                                Log.Debug(
                                    LogPrefix + "Diagnostic identifier {0}: Parsed traceparent - TraceId={1}, SpanId={2}, Sampled={3}",
                                    (object)index,
                                    parsedTraceParent.RawTraceId,
                                    parsedTraceParent.RawParentId,
                                    parsedTraceParent.Sampled);

                                // Create SpanContext from parsed W3C traceparent
                                var linkedSpanContext = new SpanContext(
                                    traceId: parsedTraceParent.TraceId,
                                    spanId: parsedTraceParent.ParentId,
                                    samplingPriority: null,
                                    serviceName: null,
                                    origin: null,
                                    rawTraceId: parsedTraceParent.RawTraceId,
                                    rawSpanId: parsedTraceParent.RawParentId,
                                    isRemote: true);

                                // Add span link with operation type attribute
                                var spanLink = new SpanLink(linkedSpanContext, attributes: [new("eventhubs.operation", "batch.add")]);
                                span.AddLink(spanLink);

                                successfulLinks++;

                                Log.Debug(
                                    LogPrefix + "Diagnostic identifier {0}: Successfully created span link to TraceId={1}, SpanId={2}",
                                    (object)index,
                                    parsedTraceParent.RawTraceId,
                                    parsedTraceParent.RawParentId);
                            }

                            if (successfulLinks > 0)
                            {
                                Log.Debug(
                                    LogPrefix + "Span linking complete: Successfully created {0} span links out of {1} diagnostic identifiers",
                                    (object)successfulLinks,
                                    (object)diagnosticIdentifiers.Count);
                            }
                            else
                            {
                                Log.Warning(
                                    LogPrefix + "Span linking failed: No valid span links created from {0} diagnostic identifiers",
                                    (object)diagnosticIdentifiers.Count);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, LogPrefix + "Error creating span links from batch diagnostic identifiers");
                    }

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
