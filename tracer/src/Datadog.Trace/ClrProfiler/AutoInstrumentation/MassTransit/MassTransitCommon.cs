// <copyright file="MassTransitCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
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
        /// Tries to get a property value from an object using reflection.
        /// Searches both direct properties and interface properties.
        /// </summary>
        /// <remarks>
        /// MassTransit's MessageConsumeContext{T} only has 'Message' as direct property,
        /// other properties like DestinationAddress come from interfaces.
        /// </remarks>
        internal static T? TryGetProperty<T>(object? obj, string propertyName)
        {
            if (obj == null)
            {
                return default;
            }

            try
            {
                var type = obj.GetType();

                // First try direct property lookup
                var property = type.GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                }

                // If not found, search interface properties
                foreach (var iface in type.GetInterfaces())
                {
                    property = iface.GetProperty(propertyName);
                    if (property != null)
                    {
                        var value = property.GetValue(obj);
                        if (value is T typedValue)
                        {
                            return typedValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitCommon.TryGetProperty: Failed to get property '{PropertyName}'", propertyName);
            }

            return default;
        }

        /// <summary>
        /// Gets the message type from a MassTransit context object.
        /// </summary>
        internal static string? GetMessageType(object? context)
        {
            if (context == null)
            {
                return null;
            }

            try
            {
                // Try generic type argument - MassTransit contexts are typically generic
                var contextType = context.GetType();
                if (contextType.IsGenericType)
                {
                    var genericArgs = contextType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        return genericArgs[0].Name;
                    }
                }

                // Try SupportedMessageTypes property
                var supportedTypes = TryGetProperty<string[]>(context, "SupportedMessageTypes");
                if (supportedTypes != null && supportedTypes.Length > 0)
                {
                    return string.Join(",", supportedTypes);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitCommon.GetMessageType: Failed to get message type");
            }

            return null;
        }

        /// <summary>
        /// Injects trace context into MassTransit SendContext headers.
        /// </summary>
        internal static void InjectTraceContext(Tracer tracer, object? sendContext, Scope scope)
        {
            if (sendContext == null || scope.Span == null)
            {
                return;
            }

            try
            {
                // Get Headers property from SendContext
                var headers = TryGetProperty<object>(sendContext, "Headers");
                if (headers == null)
                {
                    Log.Debug("MassTransitCommon.InjectTraceContext: No Headers property found");
                    return;
                }

                // Use SendContextHeadersAdapter to inject trace context
                var headersAdapter = new SendContextHeadersAdapter(headers);
                var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                tracer.TracerManager.SpanContextPropagator.Inject(context, headersAdapter);

                Log.Debug(
                    "MassTransitCommon.InjectTraceContext: Injected trace context TraceId={TraceId}",
                    scope.Span.TraceId);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitCommon.InjectTraceContext: Failed to inject trace context");
            }
        }

        /// <summary>
        /// Extracts trace context from MassTransit ReceiveContext or ConsumeContext headers.
        /// </summary>
        internal static PropagationContext ExtractTraceContext(Tracer tracer, object? receiveContext)
        {
            if (receiveContext == null)
            {
                return default;
            }

            try
            {
                // Try TransportHeaders first (ReceiveContext)
                var headers = TryGetProperty<object>(receiveContext, "TransportHeaders");

                // If not found, try Headers (ConsumeContext)
                if (headers == null)
                {
                    headers = TryGetProperty<object>(receiveContext, "Headers");
                }

                if (headers == null)
                {
                    Log.Debug("MassTransitCommon.ExtractTraceContext: No headers found");
                    return default;
                }

                // Use ContextPropagation to extract trace context from headers
                var headersAdapter = new ContextPropagation(headers);
                var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headersAdapter);

                if (extractedContext.SpanContext != null)
                {
                    Log.Debug(
                        "MassTransitCommon.ExtractTraceContext: Extracted TraceId={TraceId}, SpanId={SpanId}",
                        extractedContext.SpanContext.TraceId,
                        extractedContext.SpanContext.SpanId);
                }
                else
                {
                    Log.Debug("MassTransitCommon.ExtractTraceContext: No trace context found in headers");
                }

                return extractedContext;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitCommon.ExtractTraceContext: Failed to extract trace context");
                return default;
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
