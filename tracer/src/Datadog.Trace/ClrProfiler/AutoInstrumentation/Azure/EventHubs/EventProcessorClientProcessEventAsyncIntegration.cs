// <copyright file="EventProcessorClientProcessEventAsyncIntegration.cs" company="Datadog">
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
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Instrumentation for EventProcessorClient.ProcessEventAsync callback
    /// This instruments the internal method that invokes user's ProcessEventAsync handler
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.EventHubs",
        TypeName = "Azure.Messaging.EventHubs.EventProcessorClient",
        MethodName = "OnProcessingEventAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Azure.Messaging.EventHubs.Processor.ProcessEventArgs", ClrNames.CancellationToken },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = nameof(IntegrationId.AzureEventHubs))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class EventProcessorClientProcessEventAsyncIntegration
    {
        private const string OperationName = "azure-eventhubs.process";
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(EventProcessorClientProcessEventAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TEventArgs>(
            TTarget instance,
            TEventArgs eventArgs,
            CancellationToken cancellationToken)
            where TTarget : IEventProcessorClient, IDuckType
            where TEventArgs : IProcessEventArgs, IDuckType
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId.AzureEventHubs))
            {
                return CallTargetState.GetDefault();
            }

            Scope? scope = null;

            try
            {
                // Check if there's actually an event to process
                if (!eventArgs.HasEvent)
                {
                    Log.Debug(LogPrefix + "No event to process, skipping instrumentation");
                    return CallTargetState.GetDefault();
                }

                var eventData = eventArgs.Data;
                if (eventData.Instance == null)
                {
                    Log.Warning(LogPrefix + "EventData instance is null, cannot process");
                    return CallTargetState.GetDefault();
                }

                var partitionContext = eventArgs.Partition;
                var eventHubName = partitionContext?.EventHubName ?? instance.EventHubName;
                var consumerGroup = partitionContext?.ConsumerGroup ?? instance.ConsumerGroup;

                Log.Debug(
                    LogPrefix + "Processing event from EventHub: {0}, ConsumerGroup: {1}, Partition: {2}",
                    eventHubName,
                    consumerGroup,
                    partitionContext?.PartitionId);

                // Extract parent context from event properties
                SpanContext? parentContext = null;
                if (eventData.Properties != null)
                {
                    parentContext = AzureMessagingCommon.ExtractContext(eventData.Properties);
                    if (parentContext == null)
                    {
                        Log.Debug(LogPrefix + "No trace context found in event properties");
                    }
                }
                else
                {
                    Log.Warning(LogPrefix + "EventData.Properties is null, cannot extract trace context");
                }

                var tags = new EventHubProcessorTags
                {
                    EventHubName = eventHubName,
                    Namespace = instance.FullyQualifiedNamespace,
                    ConsumerGroup = consumerGroup,
                    Operation = "process"
                };

                if (partitionContext?.PartitionId != null)
                {
                    tags.PartitionId = partitionContext.PartitionId;
                }

                if (!string.IsNullOrEmpty(eventData.MessageId))
                {
                    tags.MessageId = eventData.MessageId;
                }

                scope = Tracer.Instance.StartActiveInternal(OperationName, parent: parentContext, tags: tags);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = $"process {eventHubName}";

                Tracer.Instance.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.AzureEventHubs);

                Log.Debug(
                    LogPrefix + "Created process span with TraceId: {0}, SpanId: {1}, Parent: {2}",
                    span.TraceId,
                    span.SpanId,
                    parentContext != null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, LogPrefix + "Error creating or populating scope for EventHub process operation");
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
                    Log.Debug(LogPrefix + "Process operation failed with exception: {ExceptionType}", exception.GetType().Name);
                }
                else
                {
                    Log.Debug(LogPrefix + "Process operation completed successfully");
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
