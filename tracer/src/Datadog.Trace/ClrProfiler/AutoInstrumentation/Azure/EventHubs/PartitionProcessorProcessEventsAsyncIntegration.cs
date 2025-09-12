// <copyright file="PartitionProcessorProcessEventsAsyncIntegration.cs" company="Datadog">
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
    /// System.Threading.Tasks.Task Microsoft.Azure.WebJobs.EventHubs.Listeners.EventHubListener/PartitionProcessor::ProcessEventsAsync(Microsoft.Azure.WebJobs.EventHubs.Processor.EventProcessorHostPartition,System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]) calltarget instrumentation
    /// </summary>
    // [InstrumentMethod(
    //     AssemblyName = "Microsoft.Azure.WebJobs.Extensions.EventHubs",
    //     TypeName = "Microsoft.Azure.WebJobs.EventHubs.Listeners.EventHubListener+PartitionProcessor",
    //     MethodName = "ProcessEventsAsync",
    //     ReturnTypeName = ClrNames.Task,
    //     ParameterTypeNames = ["Microsoft.Azure.WebJobs.EventHubs.Processor.EventProcessorHostPartition", "System.Collections.Generic.IEnumerable`1[Azure.Messaging.EventHubs.EventData]"],
    //     MinimumVersion = "6.0.0",
    //     MaximumVersion = "6.*.*",
    //     IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PartitionProcessorProcessEventsAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.receive";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PartitionProcessorProcessEventsAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TContext, TMessages>(TTarget instance, ref TContext? context, ref TMessages? messages)
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
            Log.Debug(LogPrefix + "Processing {0} EventHub messages with span links", (object)messageCount);

            var spanLinks = ExtractSpanLinksFromMessages(tracer, eventsList);
            var scope = CreateAndConfigureSpan(tracer, spanLinks, messageCount);

            // Re-inject the new span context into all messages so Azure Functions will use it as parent
            if (scope != null && eventsList.Count > 0)
            {
                ReinjectContextIntoMessages(tracer, scope, eventsList);
            }

            return new CallTargetState(scope);
        }

        internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
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

        private static List<SpanContext> ExtractSpanLinksFromMessages(Tracer tracer, List<object> messagesList)
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
                                Log.Debug(LogPrefix + "Extracted context from EventData for span link");
                            }
                        }
                    }
                }

                Log.Debug(LogPrefix + "Successfully extracted {0} context(s) for span links from EventHub messages", (object)spanLinks.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error extracting contexts for span links from EventHub messages");
            }

            return spanLinks;
        }

        private static Scope? CreateAndConfigureSpan(Tracer tracer, List<SpanContext> spanLinks, int messageCount)
        {
            try
            {
                var tags = new EventHubProducerTags
                {
                    Operation = "receive"
                };

                // Convert SpanContext list to SpanLink list for the tracer
                var links = spanLinks?.Select(ctx => new SpanLink(ctx));

                var scope = tracer.StartActiveInternal(OperationName, tags: tags, links: links);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = "receive";
                span.SetMetric("eventhubs.message_count", messageCount);

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);
                Log.Debug(LogPrefix + "Created receive span with {0} message(s) and {1} link(s)", (object)messageCount, (object)(spanLinks?.Count ?? 0));

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
    }
}
