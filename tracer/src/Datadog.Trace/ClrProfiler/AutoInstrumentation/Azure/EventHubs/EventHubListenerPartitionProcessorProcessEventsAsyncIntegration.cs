// <copyright file="EventHubListenerPartitionProcessorProcessEventsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Azure WebJobs EventHub Listener ProcessEventsAsync instrumentation for reparenting
    /// Instruments Microsoft.Azure.WebJobs.EventHubs.Listeners.EventHubListener+PartitionProcessor.ProcessEventsAsync
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Azure.WebJobs.Extensions.EventHubs",
        TypeName = "Microsoft.Azure.WebJobs.EventHubs.Listeners.EventHubListener+PartitionProcessor",
        MethodName = "ProcessEventsAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Microsoft.Azure.WebJobs.EventHubs.Processor.EventProcessorHostPartition", "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventHubListenerPartitionProcessorProcessEventsAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.receive";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubListenerPartitionProcessorProcessEventsAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TPartition, TMessages>(
            TTarget instance,
            TPartition context,
            TMessages messages)
            where TPartition : IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;
            var messagesList = messages as IEnumerable;

            if (messagesList == null)
            {
                Log.Debug(LogPrefix + "Messages parameter is not IEnumerable, skipping instrumentation");
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
            Log.Debug(LogPrefix + "Processing {0} EventHub messages for reparenting", (object)messageCount);

            var extractionResult = ExtractContextsFromMessages(tracer, eventsList);
            var scope = CreateAndConfigureSpan(tracer, extractionResult.ParentContext, extractionResult.SpanLinks, messageCount);

            // Re-inject the new span context into all messages so Azure Functions will use it as parent
            if (scope != null && eventsList.Count > 0)
            {
                ReinjectContextIntoMessages(tracer, scope, eventsList);
            }

            return new CallTargetState(scope);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
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
                    Log.Debug(LogPrefix + "ProcessEventsAsync failed with exception: {ExceptionType}", exception.GetType().Name);
                }
                else
                {
                    Log.Debug(LogPrefix + "ProcessEventsAsync completed successfully");
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
            SpanContext? parentContext = null;

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
                                parentContext ??= extractedContext; // Use first extracted context as parent
                                Log.Debug(LogPrefix + "Extracted context from EventData");
                            }
                        }
                    }
                }

                Log.Debug(LogPrefix + "Successfully extracted {0} context(s) from EventHub messages", (object)spanLinks.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error extracting contexts from EventHub messages");
            }

            return new ContextExtractionResult(parentContext, spanLinks);
        }

        private static Scope? CreateAndConfigureSpan(Tracer tracer, SpanContext? parentContext, List<SpanContext> spanLinks, int messageCount)
        {
            try
            {
                var tags = new EventHubProducerTags
                {
                    Operation = "receive"
                };

                var scope = tracer.StartActiveInternal(OperationName, parent: parentContext, tags: tags);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = "receive";
                span.SetMetric("eventhubs.message_count", messageCount);

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);
                Log.Debug(LogPrefix + "Created receive span with {0} message(s) and {1} link(s)", (object)messageCount, (object)spanLinks.Count);

                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating EventHub receive span");
                return null;
            }
        }

        private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, List<object> messagesList)
        {
            try
            {
                // Re-inject the new span context into all messages for downstream processing

                foreach (var message in messagesList)
                {
                    if (message?.TryDuckCast<IEventData>(out var eventData) == true)
                    {
                        if (eventData.Properties != null)
                        {
                            AzureMessagingCommon.InjectContext(eventData.Properties, scope);
                        }
                    }
                }

                Log.Debug(LogPrefix + "Re-injected context into {0} EventHub messages", (object)messagesList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error re-injecting context into EventHub messages");
            }
        }

        private readonly struct ContextExtractionResult
        {
            public readonly SpanContext? ParentContext;
            public readonly List<SpanContext> SpanLinks;

            public ContextExtractionResult(SpanContext? parentContext, List<SpanContext> spanLinks)
            {
                ParentContext = parentContext;
                SpanLinks = spanLinks;
            }
        }
    }
}
