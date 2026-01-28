// <copyright file="MassTransitCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Common methods for MassTransit instrumentation used by both CallTarget and DiagnosticSource approaches.
    /// </summary>
    internal static class MassTransitCommon
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitCommon));

        /// <summary>
        /// Creates a producer/send scope for outbound messages.
        /// </summary>
        internal static Scope? CreateProducerScope(
            Tracer tracer,
            string operation,
            string? destinationAddress,
            string? messageType)
        {
            Log.Debug(
                "MassTransitCommon.CreateProducerScope: operation={Operation}, destination={Destination}, messageType={MessageType}",
                operation,
                destinationAddress,
                messageType);

            var settings = tracer.CurrentTraceSettings.Settings;
            if (!settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
                Log.Debug("MassTransitCommon.CreateProducerScope: Integration is disabled");
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new MassTransitTags(SpanKinds.Producer)
                {
                    MessagingOperation = operation,
                };

                // Determine messaging system from destination
                var messagingSystem = DetermineMessagingSystem(destinationAddress);
                tags.MessagingSystem = messagingSystem;

                // Operation name: {messaging_system}.{operation}
                var operationName = $"{messagingSystem.Replace("-", "_")}.{operation}";

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Set resource name
                var cleanDestination = MassTransitIntegration.ExtractDestinationName(destinationAddress);
                if (string.IsNullOrEmpty(cleanDestination))
                {
                    cleanDestination = "unknown";
                }

                span.ResourceName = $"{cleanDestination} {operation}";

                // Set destination tags - always set DestinationName (required by span validation)
                tags.DestinationName = cleanDestination;
                if (!string.IsNullOrEmpty(destinationAddress))
                {
                    tags.DestinationAddress = destinationAddress;
                }

                // Set message type if provided
                if (!string.IsNullOrEmpty(messageType))
                {
                    tags.MessageTypes = messageType;
                }

                Log.Debug(
                    "MassTransitCommon.CreateProducerScope: Created span with TraceId={TraceId}, SpanId={SpanId}",
                    span.TraceId,
                    span.SpanId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitCommon.CreateProducerScope: Error creating producer scope");
            }

            return scope;
        }

        /// <summary>
        /// Creates a consumer scope for inbound messages.
        /// </summary>
        internal static Scope? CreateConsumerScope(
            Tracer tracer,
            string operation,
            string? inputAddress,
            string? messageType,
            PropagationContext parentContext = default)
        {
            Log.Debug(
                "MassTransitCommon.CreateConsumerScope: operation={Operation}, inputAddress={InputAddress}, messageType={MessageType}, hasParent={HasParent}",
                operation,
                inputAddress,
                messageType,
                parentContext.SpanContext != null);

            var settings = tracer.CurrentTraceSettings.Settings;
            if (!settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
                Log.Debug("MassTransitCommon.CreateConsumerScope: Integration is disabled");
                return null;
            }

            Scope? scope = null;

            try
            {
                var tags = new MassTransitTags(SpanKinds.Consumer)
                {
                    MessagingOperation = operation,
                };

                // Determine messaging system from input address
                var messagingSystem = DetermineMessagingSystem(inputAddress);
                tags.MessagingSystem = messagingSystem;

                // Consumer operations use "consumer" as the operation name (MT8 OTEL style)
                var operationName = MassTransitConstants.ConsumerOperationName;

                scope = tracer.StartActiveInternal(
                    operationName,
                    parent: parentContext.SpanContext,
                    tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Set resource name
                var cleanDestination = MassTransitIntegration.ExtractDestinationName(inputAddress);
                if (string.IsNullOrEmpty(cleanDestination))
                {
                    cleanDestination = "unknown";
                }

                span.ResourceName = $"{cleanDestination} {operation}";

                // Set destination tags - always set DestinationName (required by span validation)
                tags.DestinationName = cleanDestination;
                if (!string.IsNullOrEmpty(inputAddress))
                {
                    tags.InputAddress = inputAddress;
                }

                // Set message type if provided
                if (!string.IsNullOrEmpty(messageType))
                {
                    tags.MessageTypes = messageType;
                }

                Log.Debug(
                    "MassTransitCommon.CreateConsumerScope: Created span with TraceId={TraceId}, SpanId={SpanId}",
                    span.TraceId,
                    span.SpanId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitCommon.CreateConsumerScope: Error creating consumer scope");
            }

            return scope;
        }

        /// <summary>
        /// Determines the messaging system from the address URI scheme.
        /// </summary>
        internal static string DetermineMessagingSystem(string? address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return "in-memory";
            }

            if (address!.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase))
            {
                return "rabbitmq";
            }

            if (address.StartsWith("sb://", StringComparison.OrdinalIgnoreCase) ||
                address.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "azureservicebus";
            }

            if (address.StartsWith("amazonsqs://", StringComparison.OrdinalIgnoreCase))
            {
                return "amazonsqs";
            }

            if (address.StartsWith("kafka://", StringComparison.OrdinalIgnoreCase))
            {
                return "kafka";
            }

            if (address.StartsWith("loopback://", StringComparison.OrdinalIgnoreCase))
            {
                return "in-memory";
            }

            return "in-memory";
        }

        /// <summary>
        /// Sets error information on a span from an exception.
        /// </summary>
        internal static void SetException(Scope scope, Exception? exception)
        {
            if (scope?.Span == null || exception == null)
            {
                return;
            }

            Log.Debug("MassTransitCommon.SetException: Setting error on span: {ErrorMessage}", exception.Message);

            scope.Span.Error = true;
            scope.Span.SetTag(Tags.ErrorMsg, exception.Message);
            scope.Span.SetTag(Tags.ErrorType, exception.GetType().FullName);
            scope.Span.SetTag(Tags.ErrorStack, exception.ToString());
        }

        /// <summary>
        /// Sets additional context tags on a span.
        /// </summary>
        internal static void SetContextTags(Scope scope, Guid? messageId, Guid? conversationId, Guid? correlationId)
        {
            if (scope?.Span?.Tags is not MassTransitTags tags)
            {
                return;
            }

            if (messageId.HasValue)
            {
                tags.MessageId = messageId.Value.ToString();
            }

            if (conversationId.HasValue)
            {
                tags.ConversationId = conversationId.Value.ToString();
            }

            if (correlationId.HasValue)
            {
                tags.CorrelationId = correlationId.Value.ToString();
            }
        }

        /// <summary>
        /// Closes a scope safely with logging.
        /// </summary>
        internal static void CloseScope(Scope? scope, string operationType)
        {
            if (scope == null)
            {
                Log.Debug("MassTransitCommon.CloseScope: No scope to close for {OperationType}", operationType);
                return;
            }

            try
            {
                Log.Debug(
                    "MassTransitCommon.CloseScope: Closing {OperationType} span with TraceId={TraceId}, SpanId={SpanId}",
                    operationType,
                    scope.Span?.TraceId,
                    scope.Span?.SpanId);
                scope.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitCommon.CloseScope: Error closing scope for {OperationType}", operationType);
            }
        }
    }
}
