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
        MinimumVersion = "5.9.2",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AmqpConsumerReceiveAsyncIntegration
    {
        private const string OperationName = "receive";
        private const string SpanOperationName = "azure_eventhubs." + OperationName;
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

            if (returnValue is IReadOnlyList<object> readOnlyList && readOnlyList.Count > 0)
            {
                ProcessReceivedEvents(readOnlyList, instance);
            }

            return returnValue;
        }

        private static void ProcessReceivedEvents(IReadOnlyList<object> eventsList, IAmqpConsumer consumerInstance)
        {
            var tracer = Tracer.Instance;
            var messageCount = eventsList.Count;
            var linksEnabled = tracer.Settings.AzureEventHubsBatchLinksEnabled;

            var events = new List<object>();
            foreach (var evt in eventsList)
            {
                if (evt != null)
                {
                    events.Add(evt);
                }
            }

            var spanLinks = linksEnabled ? ExtractSpanLinksFromMessages(tracer, events) : null;
            var scope = CreateAndConfigureSpan(tracer, spanLinks, messageCount, consumerInstance, events);

            if (scope != null && events.Count > 0)
            {
                ReinjectContextIntoMessages(tracer, scope, events);
            }

            scope?.Dispose();
        }

        private static IEnumerable<SpanLink>? ExtractSpanLinksFromMessages(Tracer tracer, List<object> eventsList)
        {
            var extractedContexts = new HashSet<SpanContext>(new SpanContextComparer());

            try
            {
                foreach (var eventObj in eventsList)
                {
                    if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true &&
                        eventData.Properties != null)
                    {
                        var extractedContext = AzureMessagingCommon.ExtractContext(eventData.Properties);
                        if (extractedContext != null)
                        {
                            extractedContexts.Add(extractedContext);
                        }
                    }
                }

                var spanLinks = new List<SpanLink>(extractedContexts.Count);

                foreach (var ctx in extractedContexts)
                {
                    spanLinks.Add(new SpanLink(ctx));
                }

                return spanLinks;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting contexts for span links from EventHub messages");
            }

            return null;
        }

        private static Scope? CreateAndConfigureSpan(Tracer tracer, IEnumerable<SpanLink>? spanLinks, int messageCount, IAmqpConsumer consumerInstance, List<object> events)
        {
            var tags = Tracer.Instance.CurrentTraceSettings.Schema.Messaging.CreateAzureEventHubsTags(SpanKinds.Consumer);
            tags.MessagingOperation = OperationName;

            string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName("azureeventhubs");
            var scope = tracer.StartActiveInternal(SpanOperationName, tags: tags, serviceName: serviceName, links: spanLinks);
            var span = scope.Span;

            var eventHubName = consumerInstance.EventHubName;
            span.Type = SpanTypes.Queue;
            span.ResourceName = eventHubName;

            if (messageCount > 1)
            {
                tags.MessagingBatchMessageCount = messageCount.ToString();
            }

            if (messageCount == 1)
            {
                var eventObj = events[0];
                if (eventObj?.TryDuckCast<IEventData>(out var eventData) == true)
                {
                    var messageId = eventData.MessageId;
                    if (!string.IsNullOrEmpty(messageId))
                    {
                        tags.MessagingMessageId = messageId;
                    }
                }
            }

            var endpoint = consumerInstance.ConnectionScope?.ServiceEndpoint;
            if (endpoint != null)
            {
                tags.ServerAddress = endpoint.Host;
            }

            tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);

            return scope;
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error re-injecting context into EventHub messages");
            }
        }
    }
}
