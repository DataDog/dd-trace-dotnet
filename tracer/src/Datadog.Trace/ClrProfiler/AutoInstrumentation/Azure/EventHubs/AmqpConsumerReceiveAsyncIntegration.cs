// <copyright file="AmqpConsumerReceiveAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
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
    /// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.EventHubs.EventData]] Azure.Messaging.EventHubs.Amqp.AmqpConsumer::ReceiveAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Threading.CancellationToken) calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Amqp.AmqpConsumer",
        MethodName = "ReceiveAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.EventHubs.EventData]]",
        ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.CancellationToken],
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AmqpConsumerReceiveAsyncIntegration
    {
        private const string OperationName = "azure_eventhubs.receive";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AmqpConsumerReceiveAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, int maximumEventCount, TimeSpan? maximumWaitTime, CancellationToken cancellationToken)
        {
            return CallTargetState.GetDefault();
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
            where TTarget : IAmqpConsumer, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs, false) || exception != null)
            {
                return returnValue;
            }

            try
            {
                if (returnValue is IReadOnlyList<object> readOnlyList && readOnlyList.Count > 0)
                {
                    ProcessReceivedEvents(readOnlyList, instance.EventHubName);
                }
                else
                {
                    Log.Debug(LogPrefix + "No events received or unexpected return type");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error processing received EventHub messages in AmqpConsumer.ReceiveAsync");
            }

            return returnValue;
        }

        private static void ProcessReceivedEvents(IReadOnlyList<object> eventsList, string eventHubName)
        {
            var tracer = Tracer.Instance;
            var messageCount = eventsList.Count;
            var linksEnabled = tracer.Settings.AzureEventHubsBatchLinksEnabled;

            Log.Debug(LogPrefix + "Processing {0} EventHub messages{1}", (object)messageCount, linksEnabled ? " with span links" : " without span links");

            var events = new List<object>();
            foreach (var evt in eventsList)
            {
                if (evt != null)
                {
                    events.Add(evt);
                }
            }

            var spanLinks = linksEnabled ? ExtractSpanLinksFromMessages(tracer, events) : null;
            var scope = CreateAndConfigureSpan(tracer, spanLinks, messageCount, eventHubName);

            if (scope != null && events.Count > 0)
            {
                ReinjectContextIntoMessages(tracer, scope, events);
            }

            scope?.Dispose();
        }

        private static List<SpanContext> ExtractSpanLinksFromMessages(Tracer tracer, List<object> eventsList)
        {
            var spanLinks = new List<SpanContext>();

            try
            {
                foreach (var eventObj in eventsList)
                {
                    if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
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

        private static Scope? CreateAndConfigureSpan(Tracer tracer, List<SpanContext>? spanLinks, int messageCount, string eventHubName)
        {
            try
            {
                var tags = Tracer.Instance.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Consumer);
                tags.MessagingOperation = "receive";

                var links = spanLinks?.Select(ctx => new SpanLink(ctx));

                var scope = tracer.StartActiveInternal(OperationName, tags: tags, links: links);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = eventHubName;
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

        private static void ReinjectContextIntoMessages(Tracer tracer, Scope scope, List<object> eventsList)
        {
            try
            {
                foreach (var eventObj in eventsList)
                {
                    if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
                    {
                        if (eventData.Properties != null)
                        {
                            AzureMessagingCommon.InjectContext(eventData.Properties, scope);
                        }
                    }
                }

                Log.Debug(LogPrefix + "Re-injected context into {0} EventHub messages", (object)eventsList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error re-injecting context into EventHub messages");
            }
        }
    }
}
