// <copyright file="ProcessMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
    /// <summary>
    /// Azure.Messaging.ServiceBus.ReceiverManager.ProcessOneMessage calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Azure.Messaging.ServiceBus",
        TypeName = "Azure.Messaging.ServiceBus.ReceiverManager",
        MethodName = "ProcessOneMessage",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { "Azure.Messaging.ServiceBus.ServiceBusReceivedMessage", ClrNames.CancellationToken },
        MinimumVersion = "7.14.0",
        MaximumVersion = "7.*.*",
        IntegrationName = nameof(IntegrationId.AzureServiceBus))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProcessMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessMessageIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TMessage">Type of the message argument</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="message">Instance of the message</param>
        /// <param name="cancellationToken">CancellationToken instance</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, CancellationToken cancellationToken)
            where TTarget : IReceiverManager
            where TMessage : IServiceBusReceivedMessage
        {
            Log.Information("ProcessOneMessage starting for individual message processing");

            var tracer = Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus))
            {
                Log.Information("AzureServiceBus integration disabled, skipping ProcessOneMessage instrumentation");
                return CallTargetState.GetDefault();
            }

            Log.Information(
                "Processing individual ServiceBus message, EnqueuedTime: {EnqueuedTime}",
                message.EnqueuedTime);

            // Extract tracing context from message ApplicationProperties
            Scope? messageScope = null;
            PropagationContext extractedContext = default;

            if (message.ApplicationProperties is not null)
            {
                Log.Information(
                    "Message has {PropertyCount} ApplicationProperties, attempting context extraction",
                    (object)message.ApplicationProperties.Count);

                // Log all properties for debugging
                foreach (var prop in message.ApplicationProperties)
                {
                    Log.Information("ApplicationProperty: {Key} = {Value}", prop.Key, prop.Value);
                }

                try
                {
                    var headersCollection = new ServiceBusHeadersCollectionAdapter(message.ApplicationProperties);
                    extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headersCollection);

                    if (extractedContext.SpanContext != null)
                    {
                        Log.Information("Successfully extracted context from message ApplicationProperties");
                    }
                    else
                    {
                        Log.Information("No propagated context found in message ApplicationProperties");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated context from ServiceBus message ApplicationProperties");
                }
            }
            else
            {
                Log.Information("Message has no ApplicationProperties, cannot extract context");
            }

            // Create message processing span
            try
            {
                const string operationName = "azure.servicebus.process";
                var entityPath = instance.Processor?.EntityPath ?? "unknown";

                if (extractedContext.SpanContext != null)
                {
                    // Create span as child of extracted producer context
                    Log.Information("Creating message span with extracted parent context");
                    messageScope = tracer.StartActiveInternal(operationName, parent: extractedContext.SpanContext);
                }
                else
                {
                    // Create span without parent if no context was extracted
                    Log.Information("Creating message span without parent context (no extracted context available)");
                    messageScope = tracer.StartActiveInternal(operationName);
                }

                if (messageScope?.Span != null)
                {
                    var span = messageScope.Span;
                    span.Type = SpanTypes.Queue;
                    span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
                    span.SetTag("azure.servicebus.entity_path", entityPath);
                    span.SetTag("azure.servicebus.operation", "process");
                    // MessageId is not available in the interface, skip this tag
                    span.SetTag("messaging.system", "servicebus");
                    span.SetTag("messaging.destination.name", entityPath);
                    span.SetTag("messaging.operation", "process");

                    // Add span link to any existing batch span (if we can access it)
                    // This requires checking if there's an active scope from the batch operation
                    if (tracer.InternalActiveScope?.Span != null && tracer.InternalActiveScope != messageScope)
                    {
                        var batchSpan = tracer.InternalActiveScope.Span;
                        Log.Information(
                            "Adding span link to batch span");

                        span.AddLink(new SpanLink(batchSpan.Context));
                    }
                    else
                    {
                        Log.Information("No batch span found to link to");
                    }

                    Log.Information("Created message processing span with parent context");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating message processing span");
                messageScope?.Dispose();
                messageScope = null;
            }

            // Handle Data Streams Monitoring (preserve existing logic)
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
            if (messageScope?.Span is Span activeSpan && dataStreamsManager.IsEnabled)
            {
                Log.Information("Processing Data Streams Monitoring for message span");

                PathwayContext? pathwayContext = null;

                if (message.ApplicationProperties is not null)
                {
                    var headers = new ServiceBusHeadersCollectionAdapter(message.ApplicationProperties);

                    try
                    {
                        pathwayContext = dataStreamsManager.ExtractPathwayContextAsBase64String(headers);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting PathwayContext from Azure Service Bus message");
                    }
                }

                var consumeTime = activeSpan.StartTime.UtcDateTime;
                var produceTime = message.EnqueuedTime.UtcDateTime;
                var messageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                activeSpan.Tags.SetMetric(Trace.Metrics.MessageQueueTimeMs, messageQueueTimeMs);

                var namespaceString = instance.Processor?.EntityPath ?? "unknown";

                var edgeTags = string.IsNullOrEmpty(namespaceString)
                                    ? new[] { "direction:in", "type:servicebus" }
                                    : new[] { "direction:in", $"topic:{namespaceString}", "type:servicebus" };
                var msgSize = dataStreamsManager.IsInDefaultState ? 0 : AzureServiceBusCommon.GetMessageSize(message);
                activeSpan.SetDataStreamsCheckpoint(
                    dataStreamsManager,
                    CheckpointKind.Consume,
                    edgeTags,
                    msgSize,
                    (long)messageQueueTimeMs,
                    pathwayContext);

                Log.Information("Data Streams Monitoring completed for message span");
            }

            Log.Information("ProcessOneMessage setup completed, returning scope: {ScopeExists}", messageScope != null);
            return new CallTargetState(messageScope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            Log.Information("ProcessOneMessage ending, exception: {HasException}", exception != null);

            if (state.Scope is Scope scope)
            {
                try
                {
                    if (exception != null)
                    {
                        Log.Information(
                            "ProcessOneMessage ended with exception: {ExceptionType} - {ExceptionMessage}",
                            exception.GetType().Name,
                            exception.Message);
                        scope.Span.SetException(exception);
                    }
                    else
                    {
                        Log.Information("ProcessOneMessage completed successfully");
                    }
                }
                finally
                {
                    Log.Information("Disposing ProcessOneMessage span");
                    scope.Dispose();
                }
            }
            else
            {
                Log.Information("ProcessOneMessage ending with no scope to dispose");
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
