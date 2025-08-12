// <copyright file="ProcessMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

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
            // Do not create a span, this will automatically be created by the Azure.Messaging.ServiceBus ActivitySource(s)
            // when the following requirements are met:
            // - AzureServiceBus integration enabled
            // - DD_TRACE_OTEL_ENABLED=true
            var tracer = Tracer.Instance;
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;

            if (tracer.Settings.IsIntegrationEnabled(IntegrationId.AzureServiceBus)
                && tracer.InternalActiveScope?.Span is Span span)
            {
                // COMPREHENSIVE LOGGING FOR DEBUGGING DESTINATION NAME EXTRACTION
                LogServiceBusDebugInfo(instance, message);

                var destinationName = ExtractDestinationName(instance, message);
                Log.Information("ProcessMessageIntegration: Final destinationName='{DestinationName}'", destinationName);

                // Set messaging.destination.name tag for consumption spans
                if (!string.IsNullOrEmpty(destinationName) && span.Tags is Tagging.AzureServiceBusTags serviceBusTags)
                {
                    serviceBusTags.MessagingDestinationName = destinationName;
                }

                // Data Streams Monitoring (only if enabled)
                if (dataStreamsManager.IsEnabled)
                {
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

                    var consumeTime = span.StartTime.UtcDateTime;
                    var produceTime = message.EnqueuedTime.UtcDateTime;
                    var messageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                    span.Tags.SetMetric(Trace.Metrics.MessageQueueTimeMs, messageQueueTimeMs);

                    // TODO: we could pool these arrays to reduce allocations
                    // NOTE: the tags must be sorted in alphabetical order
                    var edgeTags = string.IsNullOrEmpty(destinationName)
                                        ? new[] { "direction:in", "type:servicebus" }
                                        : new[] { "direction:in", $"topic:{destinationName}", "type:servicebus" };
                    var msgSize = dataStreamsManager.IsInDefaultState ? 0 : AzureServiceBusCommon.GetMessageSize(message);
                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        msgSize,
                        (long)messageQueueTimeMs,
                        pathwayContext);
                }
            }

            return CallTargetState.GetDefault();
        }

        private static string? ExtractDestinationName<TTarget, TMessage>(TTarget instance, TMessage message)
            where TTarget : IReceiverManager
            where TMessage : IServiceBusReceivedMessage
        {
            // Strategy 1: Try to get from Azure Functions context if available
            var functionDestination = ExtractFromAzureFunctionsContext();
            if (!string.IsNullOrEmpty(functionDestination))
            {
                Log.Information("ProcessMessageIntegration: Using Azure Functions destination name: '{Destination}'", functionDestination);
                return functionDestination;
            }

            // Strategy 2: Parse EntityPath to extract queue/topic name
            var entityPath = instance.Processor.EntityPath;
            if (!string.IsNullOrEmpty(entityPath))
            {
                var parsedName = ParseEntityPath(entityPath);
                Log.Information("ProcessMessageIntegration: Using parsed EntityPath destination: '{Destination}' from '{EntityPath}'", parsedName, entityPath);
                return parsedName;
            }

            Log.Warning("ProcessMessageIntegration: Could not determine destination name");
            return entityPath; // Fallback to original value
        }

        private static string? ParseEntityPath(string entityPath)
        {
            if (string.IsNullOrEmpty(entityPath))
            {
                return entityPath;
            }

            // Check if this is a topic subscription (contains "/Subscriptions/")
            var subscriptionIndex = entityPath.IndexOf("/Subscriptions/", StringComparison.OrdinalIgnoreCase);
            if (subscriptionIndex >= 0)
            {
                // Extract topic name (everything before "/Subscriptions/")
                var topicName = entityPath.Substring(0, subscriptionIndex);
                Log.Information("ProcessMessageIntegration: Parsed topic name '{Topic}' from entity path '{EntityPath}'", topicName, entityPath);
                return topicName;
            }

            // Otherwise, it's probably a queue, return as-is
            Log.Information("ProcessMessageIntegration: Using queue name '{Queue}' from entity path '{EntityPath}'", entityPath, entityPath);
            return entityPath;
        }

        private static string? ExtractFromAzureFunctionsContext()
        {
            try
            {
                // Try to get current Azure Functions context
                var tracer = Tracer.Instance;
                if (tracer.InternalActiveScope?.Root?.Span?.Context?.TraceContext is not null)
                {
                    // Check if we're in an Azure Functions context
                    var span = tracer.InternalActiveScope.Root.Span;
                    if (span.Tags is Tagging.AzureFunctionsTags functionTags && functionTags.TriggerType == "ServiceBus")
                    {
                        Log.Information("ProcessMessageIntegration: Detected Azure Functions ServiceBus trigger context");

                        // Try to access the FunctionContext through the call stack or span context
                        // This is a simplified approach - in practice we'd need to store this info
                        // when the function span was created in AzureFunctionsCommon

                        // For now, return null to indicate we should try other strategies
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ProcessMessageIntegration: Error extracting from Azure Functions context");
            }

            return null;
        }

        private static void LogServiceBusDebugInfo<TTarget, TMessage>(TTarget instance, TMessage message)
            where TTarget : IReceiverManager
            where TMessage : IServiceBusReceivedMessage
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== ServiceBus ProcessMessage Debug Info ===");

                // ReceiverManager info
                sb.AppendLine($"ReceiverManager Type: {instance.GetType().FullName}");

                // Processor info
                var processor = instance.Processor;
                sb.AppendLine($"Processor Type: {processor.GetType().FullName}");
                sb.AppendLine($"Processor.EntityPath: '{processor.EntityPath}'");
                sb.AppendLine($"Processor.FullyQualifiedNamespace: '{processor.FullyQualifiedNamespace}'");
                sb.AppendLine($"Processor.Identifier: '{processor.Identifier}'");
                sb.AppendLine($"Processor.IsSessionProcessor: {processor.IsSessionProcessor}");

                // Try to access Processor.Options
                if (processor.Options is not null)
                {
                    var options = processor.Options;
                    sb.AppendLine($"Processor.Options Type: {options.GetType().FullName}");

                    // Try to duck type to get more properties
                    if (options.TryDuckCast<IServiceBusProcessorOptions>(out var optionsProxy))
                    {
                        sb.AppendLine($"Options.MaxConcurrentCalls: {optionsProxy.MaxConcurrentCalls}");
                        sb.AppendLine($"Options.ReceiveMode: {optionsProxy.ReceiveMode}");
                    }
                }

                // ReceiverManager.Receiver info
                if (instance.Receiver is not null)
                {
                    var receiver = instance.Receiver;
                    sb.AppendLine($"Receiver Type: {receiver.GetType().FullName}");

                    if (receiver.TryDuckCast<IServiceBusReceiver>(out var receiverProxy))
                    {
                        sb.AppendLine($"Receiver.EntityPath: '{receiverProxy.EntityPath}'");
                        sb.AppendLine($"Receiver.FullyQualifiedNamespace: '{receiverProxy.FullyQualifiedNamespace}'");
                    }
                }

                // Message info
                sb.AppendLine($"Message Type: {message.GetType().FullName}");
                sb.AppendLine($"Message.MessageId: '{message.MessageId}'");
                sb.AppendLine($"Message.Subject: '{message.Subject}'");
                sb.AppendLine($"Message.SessionId: '{message.SessionId}'");

                // Application Properties
                if (message.ApplicationProperties is not null)
                {
                    sb.AppendLine($"Message.ApplicationProperties Count: {message.ApplicationProperties.Count}");
                    foreach (var prop in message.ApplicationProperties)
                    {
                        sb.AppendLine($"  {prop.Key}: {prop.Value} (Type: {prop.Value?.GetType()?.Name ?? "null"})");
                    }
                }

                var debugOutput = sb.ToString();
                Log.Information("{ServiceBusDebugInfo}", debugOutput);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ProcessMessageIntegration: Error logging debug info");
            }
        }
    }
}
