// <copyright file="EventHubConsumerClientReadEventsAsyncIntegration.cs" company="Datadog">
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
    /// Instrumentation for EventHubConsumerClient.ReadEventsAsync
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.Consumer.EventHubConsumerClient",
        MethodName = "ReadEventsAsync",
        ReturnTypeName = "System.Collections.Generic.IAsyncEnumerable`1[Azure.Messaging.EventHubs.Consumer.PartitionEvent]",
        ParameterTypeNames = new[] { ClrNames.Bool, "Azure.Messaging.EventHubs.Consumer.ReadEventOptions", ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventHubConsumerClientReadEventsAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.receive";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventHubConsumerClientReadEventsAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget>(
            TTarget instance,
            bool startFromBeginning,
            object? readEventOptions,
            CancellationToken cancellationToken)
            where TTarget : IEventHubConsumerClient, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            Scope? scope = null;

            try
            {
                Log.Debug(
                    LogPrefix + "Starting receive operation for EventHub: {0}, ConsumerGroup: {1}",
                    instance.EventHubName,
                    instance.ConsumerGroup);

                var tags = new EventHubConsumerTags
                {
                    EventHubName = instance.EventHubName,
                    Namespace = instance.FullyQualifiedNamespace,
                    ConsumerGroup = instance.ConsumerGroup,
                    Operation = "receive"
                };

                scope = Tracer.Instance.StartActiveInternal(OperationName, tags: tags);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = $"receive {instance.EventHubName}";

                if (startFromBeginning)
                {
                    span.SetTag("eventhub.start_from_beginning", "true");
                }

                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);

                Log.Debug(
                    LogPrefix + "Created receive span with TraceId: {0}, SpanId: {1}",
                    span.TraceId,
                    span.SpanId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating or populating scope for EventHub receive operation");
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
                    Log.Debug(LogPrefix + "Receive operation failed with exception: {ExceptionType}", exception.GetType().Name);
                }
                else
                {
                    Log.Debug(LogPrefix + "Receive operation completed successfully");
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
