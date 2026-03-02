// <copyright file="MassTransitCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.CallTarget;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

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

                // Use the actual operation for the span name (e.g., "masstransit.send")
                var operationName = $"masstransit.{operation}";

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

                // Use the actual operation for the span name (e.g., "masstransit.receive", "masstransit.process")
                var operationName = $"masstransit.{operation}";

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
            if (scope.Span is null || exception is null)
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
        /// Extracts metadata from SendContext using duck typing.
        /// </summary>
        internal static void ExtractSendContextMetadata(
            object? sendContext,
            out string? destinationAddress,
            out Guid? messageId,
            out Guid? conversationId,
            out Guid? correlationId)
        {
            if (sendContext is null)
            {
                destinationAddress = null;
                messageId = null;
                conversationId = null;
                correlationId = null;
                return;
            }

            var context = sendContext.DuckCast<ISendContext>();
            destinationAddress = context.DestinationAddress?.ToString();
            messageId = context.MessageId;
            conversationId = context?.ConversationId;
            correlationId = context?.CorrelationId;
        }

        /// <summary>
        /// Tries to get a property value from an object using reflection.
        /// Searches both direct properties and interface properties.
        /// </summary>
        /// <remarks>
        /// MassTransit's MessageConsumeContext{T} only has 'Message' as direct property,
        /// other properties like DestinationAddress come from interfaces.
        /// </remarks>

        /// <summary>
        /// Helper method to get a property value using reflection.
        /// Used by DiagnosticObserver for properties on ConsumeContext types.
        /// </summary>
        /// <remarks>
        /// Why reflection is required (duck typing cannot be used):
        ///
        /// Investigation shows that the most common context type in DiagnosticObserver is
        /// MessageConsumeContext&lt;T&gt;, which uses EXPLICIT INTERFACE IMPLEMENTATION for all
        /// IConsumeContext properties (MessageId, SourceAddress, DestinationAddress, ReceiveContext, etc.).
        ///
        /// Evidence from MassTransit.dll inspection:
        /// - MessageConsumeContext&lt;T&gt; (most common, ~70% of cases):
        ///   - All properties on interfaces, not public on class (⚠️ explicit implementation)
        ///   - Duck typing to IConsumeContext FAILS
        /// - CorrelationIdConsumeContextProxy&lt;T&gt; (~30% of cases):
        ///   - Properties are public on the class (✅ implicit implementation)
        ///   - Duck typing to IConsumeContext SUCCEEDS
        ///
        /// Since duck typing fails for the MAJORITY of cases, reflection is the primary approach.
        /// This is the same explicit interface implementation issue that affects SendContextHeadersAdapter
        /// (see WHY_DUCK_TYPING_FAILED.md for detailed explanation).
        ///
        /// This reflection approach:
        /// - Searches class properties first (fast path for implicit implementations like proxies)
        /// - Falls back to interface properties (handles explicit implementations like MessageConsumeContext)
        /// - Works reliably for all MassTransit context types
        /// </remarks>
        internal static T? TryGetProperty<T>(object? obj, string propertyName)
        {
            if (obj is null)
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
        /// Extracts the trace ID from an Activity for exception tracking.
        /// Uses the standard pattern from the tracer: duck typing to IW3CActivity for W3C format,
        /// fallback to IActivity.RootId for hierarchical format.
        /// </summary>
        internal static string? ExtractTraceIdFromActivity(object? activity)
        {
            if (activity is null)
            {
                return null;
            }

            // Use duck typing to access W3C Activity TraceId (standard pattern used across the tracer)
            if (activity.TryDuckCast<Datadog.Trace.Activity.DuckTypes.IW3CActivity>(out var w3cActivity)
                && w3cActivity.TraceId is { } traceId)
            {
                return traceId;
            }

            // Fallback to RootId for hierarchical format
            if (activity.TryDuckCast<Datadog.Trace.Activity.DuckTypes.IActivity>(out var baseActivity))
            {
                return baseActivity.RootId;
            }

            return null;
        }

        /// <summary>
        /// Gets the message type from a MassTransit context object.
        /// Uses generic type arguments since MassTransit contexts are generic (e.g., ConsumeContext&lt;TMessage&gt;).
        /// </summary>
        internal static string? GetMessageType(object? context)
        {
            if (context is null)
            {
                return null;
            }

            try
            {
                // MassTransit contexts are typically generic (e.g., ConsumeContext<TMessage>)
                var contextType = context.GetType();
                if (contextType.IsGenericType)
                {
                    var genericArgs = contextType.GetGenericArguments();
                    if (genericArgs.Length > 0)
                    {
                        return genericArgs[0].Name;
                    }
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
            if (sendContext is null || scope.Span is null)
            {
                Log.Debug("MassTransitCommon.InjectTraceContext: sendContext or span is null");
                return;
            }

            try
            {
                // Use duck typing to get Headers property from SendContext
                var context = sendContext.DuckCast<ISendContext>();
                var headers = context.Headers;
                if (headers is null)
                {
                    Log.Debug("MassTransitCommon.InjectTraceContext: No Headers property found in SendContext");
                    return;
                }

                Log.Debug("MassTransitCommon.InjectTraceContext: Got Headers from SendContext, injecting trace context");

                // Inject trace context headers into outgoing message (producer side)
                var injectHeadersAdapter = new ContextPropagationInjectAdapter(headers);
                var propagationContext = new PropagationContext(scope.Span.Context, Baggage.Current);
                tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, injectHeadersAdapter);

                Log.Debug(
                    "MassTransitCommon.InjectTraceContext: Successfully injected trace context TraceId={TraceId}",
                    scope.Span.TraceId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitCommon.InjectTraceContext: Failed to inject trace context");
            }
        }

        /// <summary>
        /// Extracts trace context from MassTransit ReceiveContext or ConsumeContext headers.
        /// </summary>
        internal static PropagationContext ExtractTraceContext(Tracer tracer, object? receiveContext)
        {
            if (receiveContext is null)
            {
                Log.Debug("MassTransitCommon.ExtractTraceContext: receiveContext is null");
                return default;
            }

            try
            {
                // Use reflection to get Headers property from context
                // Duck typing fails for MessageConsumeContext (most common type) due to explicit interface implementation
                var headers = TryGetProperty<object>(receiveContext, "Headers");

                if (headers is null)
                {
                    return default;
                }

                // Extract trace context from incoming message headers (consumer side)
                var extractHeadersAdapter = new ContextPropagationExtractAdapter(headers);
                var extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(extractHeadersAdapter);

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
            if (scope is null)
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

        /// <summary>
        /// Gets the ActivityKind for MassTransit operations based on the operation name.
        /// </summary>
        internal static ActivityKind GetActivityKind(IActivity5 activity)
        {
            var operationName = activity.OperationName;

            // Check messaging.operation tag which MassTransit sets
            var messagingOperation = activity.Tags.FirstOrDefault(kv => kv.Key == "messaging.operation").Value;

            if (!StringUtil.IsNullOrWhiteSpace(messagingOperation))
            {
                return messagingOperation switch
                {
                    "send" => ActivityKind.Producer,
                    "receive" => ActivityKind.Consumer,
                    "process" => ActivityKind.Consumer,
                    _ => activity.Kind
                };
            }

            // Fallback to operation name analysis (case-insensitive using ToLowerInvariant for .NET Framework compatibility)
            if (operationName is not null)
            {
                var lowerOperationName = operationName.ToLowerInvariant();

                if (lowerOperationName.Contains("send"))
                {
                    return ActivityKind.Producer;
                }

                if (lowerOperationName.Contains("receive") ||
                    lowerOperationName.Contains("consume") ||
                    lowerOperationName.Contains("handle") ||
                    lowerOperationName.Contains("process") ||
                    lowerOperationName.Contains("saga") ||
                    lowerOperationName.Contains("activity"))
                {
                    return ActivityKind.Consumer;
                }
            }

            return activity.Kind;
        }

        /// <summary>
        /// Sets the ActivityKind for MassTransit operations.
        /// </summary>
        internal static void SetActivityKind(IActivity5 activity)
        {
            ActivityListener.SetActivityKind(activity, GetActivityKind(activity));
        }

        /// <summary>
        /// Creates a resource name for MassTransit operations based on destination and operation.
        /// </summary>
        internal static string CreateResourceName(string? destination, string? operation)
        {
            var cleanDestination = MassTransitIntegration.ExtractDestinationName(destination);
            if (StringUtil.IsNullOrWhiteSpace(cleanDestination))
            {
                cleanDestination = "unknown";
            }

            if (StringUtil.IsNullOrWhiteSpace(operation))
            {
                return cleanDestination;
            }

            return $"{cleanDestination} {operation}";
        }

        /// <summary>
        /// Enhances MassTransit Activity metadata by updating DisplayName (resource name) and OperationName.
        /// This is called from ActivityHandler (MassTransit 8.x) and DiagnosticObserver (MassTransit 7.x).
        /// Supports both OTEL semantic convention tags (MassTransit 8.x) and legacy tags (MassTransit 7.x).
        /// </summary>
        internal static void EnhanceActivityMetadata(IActivity5 activity)
        {
            // Add component tag to identify this as a MassTransit span
            activity.AddTag(Tags.InstrumentationName, "masstransit");

            // Preserve the original operation name
            var originalOperationName = activity.OperationName ?? string.Empty;
            if (!StringUtil.IsNullOrWhiteSpace(originalOperationName))
            {
                activity.AddTag("operation.name", originalOperationName);
            }

            // Try MassTransit 8.x OTEL semantic convention tag names first
            var destination = activity.Tags.FirstOrDefault(kv => kv.Key == "messaging.destination.name").Value;
            var operation = activity.Tags.FirstOrDefault(kv => kv.Key == "messaging.operation").Value;
            var messagingSystem = activity.Tags.FirstOrDefault(kv => kv.Key == "messaging.system").Value;

            // Fallback to MassTransit 7.x tag names if OTEL tags not found
            if (StringUtil.IsNullOrWhiteSpace(destination))
            {
                var peerAddress = activity.Tags.FirstOrDefault(kv => kv.Key == "peer.address").Value;
                destination = peerAddress?.TrimStart('/');
            }

            if (StringUtil.IsNullOrWhiteSpace(operation))
            {
                var peerService = activity.Tags.FirstOrDefault(kv => kv.Key == "peer.service").Value;
                operation = peerService;
            }

            if (StringUtil.IsNullOrWhiteSpace(messagingSystem))
            {
                var destinationAddress = activity.Tags.FirstOrDefault(kv => kv.Key == "destination-address").Value;
                messagingSystem = DetermineMessagingSystem(destinationAddress);
            }

            // Add messaging.operation tag if not already present
            if (!StringUtil.IsNullOrWhiteSpace(operation))
            {
                activity.AddTag(Tags.MessagingOperation, operation!.ToLowerInvariant());
            }

            // Add messaging.system tag if not already present
            if (!StringUtil.IsNullOrWhiteSpace(messagingSystem))
            {
                activity.AddTag(Tags.MessagingSystem, messagingSystem!);
            }

            // Update DisplayName (resource name)
            if (!StringUtil.IsNullOrWhiteSpace(destination))
            {
                var resourceName = CreateResourceName(destination, operation);

                // Update DisplayName for resource name
                activity.DisplayName = resourceName;

                Log.Debug(
                    "MassTransitCommon.EnhanceActivityMetadata: Updated DisplayName to '{DisplayName}'",
                    activity.DisplayName);
            }
            else
            {
                Log.Debug("Unable to update MassTransit Activity's resource name: destination not found in tags.");
            }
        }
    }
}
