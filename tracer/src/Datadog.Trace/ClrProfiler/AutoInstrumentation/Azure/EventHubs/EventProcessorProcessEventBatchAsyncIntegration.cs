// <copyright file="EventProcessorProcessEventBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Instrumentation for EventProcessor{TPartition}.ProcessEventBatchAsync
    /// This method is the central batch processing method that handles all EventProcessor scenarios.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Primitives.EventProcessor`1",
        MethodName = "ProcessEventBatchAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = ["!0", "System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.EventHubs.EventData]", ClrNames.Bool, ClrNames.CancellationToken],
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventProcessorProcessEventBatchAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.receive";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventProcessorProcessEventBatchAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TPartition, TEventBatch>(
            TTarget instance,
            TPartition partition,
            TEventBatch eventBatch,
            bool dispatchEmptyBatches,
            CancellationToken cancellationToken)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs, false))
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;
            var messagesList = eventBatch as IEnumerable;

            if (messagesList == null)
            {
                Log.Debug(LogPrefix + "EventBatch parameter is not IEnumerable, skipping instrumentation");
                return CallTargetState.GetDefault();
            }

            // Convert to list to get count and iterate multiple times
            var eventsList = new List<object>();
            foreach (var message in messagesList)
            {
                if (message != null)
                {
                    eventsList.Add(message);
                }
            }

            var messageCount = eventsList.Count;

            // Skip instrumentation for empty batches unless they should be dispatched
            if (messageCount == 0 && !dispatchEmptyBatches)
            {
                Log.Debug(LogPrefix + "Empty batch with dispatchEmptyBatches=false, skipping instrumentation");
                return CallTargetState.GetDefault();
            }

            Log.Debug(LogPrefix + "Processing EventProcessor batch with {0} EventHub messages for linking", (object)messageCount);

            var extractionResult = ExtractContextsFromMessages(tracer, eventsList);
            var scope = CreateBatchSpanWithLinks(tracer, extractionResult.SpanLinks, messageCount);

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
                    Log.Debug(LogPrefix + "ProcessEventBatchAsync failed with exception: {ExceptionType}", exception.GetType().Name);
                }
                else
                {
                    Log.Debug(LogPrefix + "ProcessEventBatchAsync completed successfully");
                }
            }
            finally
            {
                scope.Dispose();
            }

            return returnValue;
        }

        private static ContextExtractionResult ExtractContextsFromMessages(Tracer tracer, List<object> messagesList)
        {
            var spanLinks = new List<SpanContext>();

            try
            {
                foreach (var message in messagesList)
                {
                    if (message?.TryDuckCast<IEventData>(out var eventData) == true)
                    {
                        if (eventData.Properties != null)
                        {
                            var extractedContext = AzureMessagingCommon.ExtractContext(eventData.Properties);
                            if (extractedContext != null)
                            {
                                spanLinks.Add(extractedContext);
                                Log.Debug(LogPrefix + "Extracted context from EventData for span linking");
                            }
                        }
                    }
                }

                Log.Debug(LogPrefix + "Successfully extracted {0} context(s) from EventHub messages for span linking", (object)spanLinks.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error extracting contexts from EventHub messages");
            }

            return new ContextExtractionResult(spanLinks);
        }

        private static Scope? CreateBatchSpanWithLinks(Tracer tracer, List<SpanContext> spanLinks, int messageCount)
        {
            try
            {
                var tags = new EventHubConsumerTags
                {
                    Operation = "receive"
                };

                // Convert SpanContext list to SpanLink list for the tracer
                var links = spanLinks?.Select(ctx => new SpanLink(ctx));

                // Create the batch reception span with links to individual message spans
                var scope = tracer.StartActiveInternal(OperationName, tags: tags, links: links);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = "receive batch";
                span.SetMetric("eventhubs.batch.message_count", messageCount);

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);
                Log.Debug(
                    LogPrefix + "Created batch receive span with {0} message(s) and {1} link(s)",
                    (object)messageCount,
                    (object)(spanLinks?.Count ?? 0));

                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating EventHub batch receive span");
                return null;
            }
        }

        private readonly struct ContextExtractionResult
        {
            public readonly List<SpanContext> SpanLinks;

            public ContextExtractionResult(List<SpanContext> spanLinks)
            {
                SpanLinks = spanLinks;
            }
        }
    }
}
