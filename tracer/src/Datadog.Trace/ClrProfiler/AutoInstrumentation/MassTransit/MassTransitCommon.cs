// <copyright file="MassTransitCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.DuckTyping;
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
        /// Creates a produce (send) span for an outbound message.
        /// </summary>
        internal static Scope? CreateProduceSpan(Tracer tracer, string? destinationAddress)
            => CreateProducerScope(tracer, MassTransitConstants.OperationSend, MassTransitConstants.SendOperationName, destinationAddress);

        /// <summary>
        /// Creates a receive span for an inbound message at the transport level.
        /// </summary>
        internal static Scope? CreateReceiveSpan(Tracer tracer, string? inputAddress, PropagationContext parentContext)
            => CreateConsumerScope(tracer, MassTransitConstants.OperationReceive, MassTransitConstants.ReceiveOperationName, inputAddress, messageType: null, parentContext);

        /// <summary>
        /// Creates a process span for message processing by a consumer, handler, or saga.
        /// </summary>
        internal static Scope? CreateProcessSpan(Tracer tracer, string? inputAddress, string? messageType, PropagationContext parentContext)
            => CreateConsumerScope(tracer, MassTransitConstants.OperationProcess, MassTransitConstants.ProcessOperationName, inputAddress, messageType, parentContext);

        /// <summary>
        /// Creates a producer/send scope for outbound messages.
        /// </summary>
        internal static Scope? CreateProducerScope(
            Tracer tracer,
            string operation,
            string operationName,
            string? destinationAddress)
        {
            var settings = tracer.CurrentTraceSettings.Settings;
            if (!settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
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

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Set resource name
                var cleanDestination = ExtractDestinationName(destinationAddress);
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
            string operationName,
            string? inputAddress,
            string? messageType,
            PropagationContext parentContext = default)
        {
            var settings = tracer.CurrentTraceSettings.Settings;
            if (!settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
            {
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

                scope = tracer.StartActiveInternal(
                    operationName,
                    parent: parentContext.SpanContext,
                    tags: tags);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Set resource name
                var cleanDestination = ExtractDestinationName(inputAddress);
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
            if (StringUtil.IsNullOrWhiteSpace(address))
            {
                return "unknown";
            }

            if (address.StartsWith("rabbitmq://", StringComparison.OrdinalIgnoreCase))
            {
                return "rabbitmq";
            }

            // this scenario is untested
            if (address.StartsWith("sb://", StringComparison.OrdinalIgnoreCase))
            {
                return "azureservicebus";
            }

            if (address.StartsWith("amazonsqs://", StringComparison.OrdinalIgnoreCase))
            {
                return "sqs";
            }

            if (address.StartsWith("kafka://", StringComparison.OrdinalIgnoreCase))
            {
                return "kafka";
            }

            if (address.StartsWith("loopback://", StringComparison.OrdinalIgnoreCase))
            {
                return "in-memory";
            }

            Log.Debug("Unable to determine messaging system for address: {DestinationAddress}", address);
            return "unknown";
        }

        /// <summary>
        /// Sets additional context tags on a span.
        /// </summary>
        internal static void SetContextTags(Scope scope, Guid? messageId, Guid? conversationId, Guid? correlationId, Guid? initiatorId)
        {
            if (scope?.Span?.Tags is not MassTransitTags tags)
            {
                // All MassTransit scopes are created via Create{Producer,Consumer}Scope with MassTransitTags,
                // so reaching this branch means the scope was created somewhere unexpected.
                Log.Debug("MassTransitCommon.SetContextTags: Active scope's tags are not MassTransitTags; skipping.");
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

            if (initiatorId.HasValue)
            {
                tags.InitiatorId = initiatorId.Value.ToString();
            }
        }

        /// <summary>
        /// Extracts metadata from SendContext using duck typing.
        /// Returns the duck-typed proxy so the caller can reuse it (e.g. for header injection)
        /// without casting again.
        /// </summary>
        internal static MessageSendContextStruct? ExtractSendContextMetadata(
            object? sendContext,
            out string? destinationAddress,
            out Guid? messageId,
            out Guid? conversationId,
            out Guid? correlationId,
            out Guid? initiatorId)
        {
            if (sendContext is null || !sendContext.TryDuckCast<MessageSendContextStruct>(out var context))
            {
                // Either no SendContext or its shape diverges from MessageSendContextStruct. Returning
                // null lets the caller create a producer span without metadata rather than throwing.
                destinationAddress = null;
                messageId = null;
                conversationId = null;
                correlationId = null;
                initiatorId = null;
                return null;
            }

            destinationAddress = context.DestinationAddress?.ToString();
            messageId = context.MessageId;
            conversationId = context.ConversationId;
            correlationId = context.CorrelationId;
            initiatorId = context.InitiatorId;
            return context;
        }

        /// <summary>
        /// Resolves the message type for an inbound message from an already duck-typed consume context.
        /// Returns null when <paramref name="consumeContext"/> is null (cast failed) or
        /// <c>SupportedMessageTypes</c> is empty.
        /// </summary>
        internal static string? GetConsumeMessageType(IConsumeContext? consumeContext)
        {
            return consumeContext?.SupportedMessageTypes is { } types ? JoinMessageTypes(types) : null;
        }

        private static string? JoinMessageTypes(IEnumerable enumerable)
        {
            List<string>? messageTypes = null;

            foreach (var item in enumerable)
            {
                if (item is string messageType && !StringUtil.IsNullOrWhiteSpace(messageType))
                {
                    messageTypes ??= [];
                    messageTypes.Add(messageType);
                }
            }

            return messageTypes is { Count: > 0 }
                       ? string.Join(",", messageTypes)
                       : null;
        }

        /// <summary>
        /// Injects trace context into MassTransit SendContext headers.
        /// Accepts the already duck-typed proxy to avoid casting sendContext again.
        /// </summary>
        internal static void InjectTraceContext(Tracer tracer, MessageSendContextStruct? sendContext, Scope scope)
        {
            if (sendContext is null || scope.Span is null)
            {
                return;
            }

            var propagationContext = new PropagationContext(scope.Span.Context, Baggage.Current);

            try
            {
                var internalHeaders = sendContext.Value.Headers?.Headers;
                if (internalHeaders != null)
                {
                    var adapter = new CarrierWithDelegate<IDictionary<string, object>>(
                        internalHeaders,
                        setter: (d, k, v) => d[k] = v);
                    tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, adapter);
                }
                else
                {
                    Log.Debug("MassTransitCommon.InjectTraceContext: Could not inject — Headers null");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MassTransitCommon.InjectTraceContext: Failed to inject trace context");
            }
        }

        /// <summary>
        /// Extracts a clean destination name from a MassTransit address URI.
        /// For URN format destinations, keeps the full URN.
        /// For queue/endpoint destinations, extracts just the name.
        /// Special endpoints are normalized: "_bus_xxx" -> "bus", "_endpoint_xxx" -> "endpoint"
        /// </summary>
        internal static string ExtractDestinationName(string? fullAddress)
        {
            if (StringUtil.IsNullOrWhiteSpace(fullAddress))
            {
                return "unknown";
            }

            // Handle direct URN format (urn:message:Namespace:MessageType)
            if (fullAddress!.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
            {
                return fullAddress;
            }

            string entityName = fullAddress;
            try
            {
                if (Uri.TryCreate(fullAddress, UriKind.Absolute, out var uri))
                {
                    var path = uri.AbsolutePath.TrimStart('/');
                    if (!StringUtil.IsNullOrWhiteSpace(path))
                    {
                        if (path.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
                        {
                            return path;
                        }

                        var lastSlash = path.LastIndexOf('/');
                        entityName = lastSlash >= 0 && lastSlash < path.Length - 1
                            ? path.Substring(lastSlash + 1)
                            : path;
                    }
                }
            }
            catch
            {
                // Continue with fullAddress as entityName
            }

            if (entityName.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
            {
                return entityName;
            }

            if (entityName.IndexOf("_bus_", StringComparison.Ordinal) >= 0) { return "bus"; }
            if (entityName.IndexOf("_endpoint_", StringComparison.Ordinal) >= 0) { return "endpoint"; }
            if (entityName.IndexOf("_signalr_", StringComparison.Ordinal) >= 0) { return "signalr"; }
            if (entityName.StartsWith("Instance_", StringComparison.Ordinal)) { return "instance"; }

            return entityName;
        }
    }
}
