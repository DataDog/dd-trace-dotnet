// <copyright file="EventDataBatchTryAddIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Azure.Messaging.EventHubs.Producer.EventDataBatch.TryAdd calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Producer.EventDataBatch",
        MethodName = "TryAdd",
        ReturnTypeName = ClrNames.Bool,
        ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.EventData" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventDataBatchTryAddIntegration
    {
        private const string OperationName = "azure-eventhubs.batch.add";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventDataBatchTryAddIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEventData>(
            TTarget instance,
            TEventData eventData)
            where TEventData : IEventData, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs, false) ||
                !Tracer.Instance.Settings.AzureEventHubsBatchLinksEnabled)
            {
                return CallTargetState.GetDefault();
            }

            var tags = new EventHubProducerTags
            {
                Operation = "batch.add"
            };

            var scope = Tracer.Instance.StartActiveInternal(OperationName, tags: tags);
            var span = scope.Span;

            span.Type = SpanTypes.Queue;
            span.ResourceName = "batch.add";

            if (eventData?.Instance != null)
            {
                if (!string.IsNullOrEmpty(eventData.MessageId))
                {
                    span.SetTag("messaging.message_id", eventData.MessageId);
                }
            }

            return new CallTargetState(scope);
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(
            TTarget instance,
            TReturn returnValue,
            Exception? exception,
            in CallTargetState state)
        {
            if (exception == null && returnValue is bool success && success && state.Scope?.Span?.Context != null && instance != null)
            {
                EventHubsCommon.StoreSpanContext(instance, state.Scope.Span.Context);
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
