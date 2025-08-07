// <copyright file="ServiceBusReceiverReceiveMessagesAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.ServiceBus
{
/// <summary>
/// System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]] Azure.Messaging.ServiceBus.ServiceBusReceiver::ReceiveMessagesAsync(System.Int32,System.Nullable`1[System.TimeSpan],System.Boolean,System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Azure.Messaging.ServiceBus",
    TypeName = "Azure.Messaging.ServiceBus.ServiceBusReceiver",
    MethodName = "ReceiveMessagesAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1[System.Collections.Generic.IReadOnlyList`1[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]",
    ParameterTypeNames = [ClrNames.Int32, "System.Nullable`1[System.TimeSpan]", ClrNames.Bool, ClrNames.CancellationToken],
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AzureServiceBus))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ServiceBusReceiverReceiveMessagesAsyncIntegration
{
    private const string OperationName = "azure.servicebus.receive";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ServiceBusReceiverReceiveMessagesAsyncIntegration));

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref int maxMessages, ref TimeSpan? maxWaitTime, ref bool isProcessor, ref CancellationToken cancellationToken)
    {
        Log.Information("ReceiveMessagesAsync running");

        // Log the full call stack to understand who's calling this
        var stackTrace = new StackTrace(true);
        var frames = stackTrace.GetFrames();

        Log.Information("=== FULL CALL STACK FOR ReceiveMessagesAsync ===");
        if (frames != null)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var method = frame?.GetMethod();
                var fileName = frame?.GetFileName();
                var lineNumber = frame?.GetFileLineNumber() ?? 0;

                if (method != null)
                {
                    var declaringType = method.DeclaringType?.FullName ?? "Unknown";
                    var methodName = method.Name;
                    var fullFrameInfo = $"  [{i}] {declaringType}.{methodName}";
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        fullFrameInfo += $" at {Path.GetFileName(fileName)}:{lineNumber}";
                    }

                    Log.Information("{FrameInfo}", fullFrameInfo);
                }
            }
        }

        Log.Information("=== END CALL STACK ===");

        var tracer = Tracer.Instance;
        var scope = tracer.StartActiveInternal(OperationName);
        var span = scope.Span;
        span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
        span.SetTag("azure.servicebus.entity_path", "entity_path");
        span.SetTag("azure.servicebus.namespace", "namespace");
        span.SetTag("azure.servicebus.operation", "receive_batch");
        return new CallTargetState(scope);
    }

    internal static TReturn? OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn? returnValue, Exception exception, in CallTargetState state)
    {
        Log.Information("ReceiveMessagesAsync ending with return type: {ReturnType}, actual return value type: {ActualType}", typeof(TReturn).FullName, returnValue?.GetType().FullName ?? "null");

        Scope? scope = state.Scope;

        if (scope is null)
        {
            Log.Information("Scope is null, ending without processing messages");
            return returnValue;
        }

        var tracer = Tracer.Instance;

        try
        {
            if (exception != null)
            {
                Log.Information(
                    "ReceiveMessagesAsync ended with exception: {ExceptionType} - {ExceptionMessage}",
                    exception.GetType().Name,
                    exception.Message);
                scope.Span.SetException(exception);
            }
            else if (returnValue != null && GetCollectionCount(returnValue) > 0)
            {
                var messageCount = GetCollectionCount(returnValue);
                Log.Information("ReceiveMessagesAsync completed successfully with {MessageCount} messages", (object)messageCount);

                // For each message in the batch, extract context and create individual spans
                Log.Information("Attempting to iterate over returnValue of type {Type}", returnValue?.GetType().FullName ?? "null");

                // Try to treat returnValue as IEnumerable directly since it's IReadOnlyList<ServiceBusReceivedMessage>
                if (returnValue is System.Collections.IEnumerable enumerable)
                {
                    foreach (var messageObj in enumerable)
                    {
                        // Duck cast each individual message to our interface
                        if (!messageObj.TryDuckCast<IServiceBusReceivedMessage>(out var message))
                        {
                            Log.Warning("Failed to duck cast individual message of type {MessageType} to IServiceBusReceivedMessage", messageObj?.GetType().FullName ?? "null");
                            continue;
                        }

                        try
                        {
                        Log.Information(
                            "Processing batch message - EnqueuedTime: {EnqueuedTime}",
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
                                    Log.Information("Successfully extracted context from batch message ApplicationProperties");
                                }
                                else
                                {
                                    Log.Information("No propagated context found in batch message ApplicationProperties");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error extracting propagated context from batch message ApplicationProperties");
                            }
                        }
                        else
                        {
                            Log.Information("Batch message has no ApplicationProperties, cannot extract context");
                        }

                        // Create individual message processing span
                        const string messageOperationName = "azure.servicebus.process_batch_message";
                        var entityPath = GetEntityPath(instance) ?? "unknown";

                        if (extractedContext.SpanContext != null)
                        {
                            // Create span as child of extracted producer context
                            Log.Information("Creating batch message span with extracted parent context");
                            messageScope = tracer.StartActiveInternal(messageOperationName, parent: extractedContext.SpanContext);
                        }
                        else
                        {
                            // Create span without parent if no context was extracted
                            Log.Information("Creating batch message span without parent context (no extracted context available)");
                            messageScope = tracer.StartActiveInternal(messageOperationName);
                        }

                        if (messageScope?.Span != null)
                        {
                            var messageSpan = messageScope.Span;
                            messageSpan.Type = SpanTypes.Queue;
                            messageSpan.SetTag(Tags.SpanKind, SpanKinds.Consumer);
                            messageSpan.SetTag("azure.servicebus.entity_path", entityPath);
                            messageSpan.SetTag("azure.servicebus.operation", "process_batch_message");
                            // MessageId is not available in the interface, skip this tag
                            messageSpan.SetTag("messaging.system", "servicebus");
                            messageSpan.SetTag("messaging.destination.name", entityPath);
                            messageSpan.SetTag("messaging.operation", "process");
                            messageSpan.SetTag("azure.servicebus.batch_processing", "true");

                            // Add span link back to the batch span for correlation
                            Log.Information(
                                "Adding span link from message to batch span");

                            messageSpan.AddLink(new SpanLink(scope.Span.Context));

                            Log.Information("Created batch message processing span with parent context");

                            // Handle Data Streams Monitoring for batch message
                            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                            if (dataStreamsManager.IsEnabled)
                            {
                                Log.Information("Processing Data Streams Monitoring for batch message span");

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
                                        Log.Error(ex, "Error extracting PathwayContext from batch message");
                                    }
                                }

                                var consumeTime = messageSpan.StartTime.UtcDateTime;
                                var produceTime = message.EnqueuedTime.UtcDateTime;
                                var messageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                                messageSpan.Tags.SetMetric(Trace.Metrics.MessageQueueTimeMs, messageQueueTimeMs);

                                var edgeTags = string.IsNullOrEmpty(entityPath)
                                                    ? new[] { "direction:in", "type:servicebus", "batch:true" }
                                                    : new[] { "direction:in", $"topic:{entityPath}", "type:servicebus", "batch:true" };
                                var msgSize = dataStreamsManager.IsInDefaultState ? 0 : AzureServiceBusCommon.GetMessageSize(message);
                                messageSpan.SetDataStreamsCheckpoint(
                                    dataStreamsManager,
                                    CheckpointKind.Consume,
                                    edgeTags,
                                    msgSize,
                                    (long)messageQueueTimeMs,
                                    pathwayContext);

                                Log.Information("Data Streams Monitoring completed for batch message span");
                            }

                            // Immediately finish the individual message span since we're not tracking its actual processing
                            Log.Information("Finishing batch message span");
                            messageScope.Dispose();
                        }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error processing individual message in batch");
                        }
                    }
                }
                else
                {
                    Log.Warning("Return value is not IEnumerable, cannot process batch messages. Actual type: {ActualType}", returnValue?.GetType().FullName ?? "null");
                }

                // Update batch span with message count
                var finalMessageCount = GetCollectionCount(returnValue);
                scope.Span.SetTag("azure.servicebus.message_count", finalMessageCount.ToString());
                scope.Span.SetTag("messaging.batch.message_count", finalMessageCount.ToString());
            }
            else
            {
                Log.Information("ReceiveMessagesAsync completed with no messages or null return value");
                scope.Span.SetTag("azure.servicebus.message_count", "0");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnAsyncMethodEnd for ReceiveMessagesAsync");
        }
        finally
        {
            Log.Information("Disposing batch ReceiveMessagesAsync span");
            scope.Dispose();
        }

        return returnValue;
    }

    private static string? GetEntityPath<TTarget>(TTarget instance)
    {
        try
        {
            // Try to extract entity path from the ServiceBusReceiver instance
            var entityPathProperty = typeof(TTarget).GetProperty("EntityPath");
            return entityPathProperty?.GetValue(instance) as string;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not extract EntityPath from ServiceBusReceiver instance");
            return null;
        }
    }

    private static int GetCollectionCount<T>(T? collection)
    {
        if (collection == null)
        {
            return 0;
        }

        // Try to get Count property via reflection (IReadOnlyList<T> should have this)
        try
        {
            var countProperty = collection.GetType().GetProperty("Count");
            if (countProperty?.GetValue(collection) is int count)
            {
                return count;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not get Count property from collection of type: {CollectionType}", collection.GetType().FullName);
        }

        // Fallback: enumerate to count if it's IEnumerable
        try
        {
            if (collection is System.Collections.IEnumerable enumerable)
            {
                int count = 0;
                foreach (var item in enumerable)
                {
                    count++;
                }

                return count;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not enumerate collection to count items");
        }

        return 0;
    }
}
}
