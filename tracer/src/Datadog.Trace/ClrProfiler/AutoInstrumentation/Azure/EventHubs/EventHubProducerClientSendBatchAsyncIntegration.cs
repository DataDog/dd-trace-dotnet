// <copyright file="EventHubProducerClientSendBatchAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

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
        internal static CallTargetState OnMethodBegin<TTarget, TEventBatch>(
            TTarget instance,
            TEventBatch eventBatch,
            CancellationToken cancellationToken)
            where TTarget : IEventHubProducerClient, IDuckType
            where TEventBatch : IEventDataBatch, IDuckType
        {
            var spanContexts = EventHubsCommon.RetrieveAndClearSpanContexts(eventBatch?.Instance);
            var spanLinks = spanContexts?.Select(ctx => new SpanLink(ctx));
            var messageCount = eventBatch?.Instance != null ? eventBatch.Count : (int?)null;

            return EventHubsCommon.CreateSenderSpan(
                instance,
                messages: null,
                messageCount: messageCount,
                spanLinks: spanLinks);
        }

        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            return EventHubsCommon.OnAsyncMethodEnd(returnValue, exception, in state);
        }
    }
}
