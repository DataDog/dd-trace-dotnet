// <copyright file="MassTransitIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal static class MassTransitIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitIntegration));

        internal static Scope? CreateProducerScope(
            Tracer tracer,
            string operation,
            string? messageType,
            string? destinationName = null,
            DateTimeOffset? startTime = null,
            string? messagingSystem = null)
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
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

                // Determine messaging system from destination or use provided value or default to in-memory
                var resolvedMessagingSystem = messagingSystem ?? DetermineMessagingSystem(destinationName);
                tags.MessagingSystem = resolvedMessagingSystem;

                // MT8 OTEL-style operation name: {messaging_system}.{operation}
                // e.g., "in_memory.send", "rabbitmq.send"
                var operationName = $"{resolvedMessagingSystem.Replace("-", "_")}.{operation}";

                // Don't specify serviceName to use the default tracer service name (like Hangfire)
                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Resource name uses clean destination name: "{clean_destination} {operation}"
                // e.g., "submit-order send" instead of "loopback://localhost/submit-order send"
                if (destinationName != null)
                {
                    var cleanDestination = ExtractDestinationName(destinationName);
                    span.ResourceName = $"{cleanDestination} {operation}";
                    // MT8 OTEL: messaging.destination.name = clean name, messaging.masstransit.destination_address = full URI
                    tags.DestinationName = cleanDestination;
                    tags.DestinationAddress = destinationName;
                }
                else if (messageType != null)
                {
                    span.ResourceName = $"{messageType} {operation}";
                    tags.DestinationName = messageType;
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
                }

                // Set message type if provided
                if (messageType != null)
                {
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating MassTransit producer scope");
            }

            return scope;
        }

        internal static Scope? CreateConsumerScope(
            Tracer tracer,
            string operation,
            string? messageType,
            PropagationContext context = default,
            DateTimeOffset? startTime = null,
            string? destinationName = null,
            string? messagingSystem = null)
        {
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(MassTransitConstants.IntegrationId))
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

                // Determine messaging system from destination or use provided value or default to in-memory
                var resolvedMessagingSystem = messagingSystem ?? DetermineMessagingSystem(destinationName) ?? "in-memory";
                tags.MessagingSystem = resolvedMessagingSystem;

                // MT8 OTEL-style operation name for consumer: "consumer"
                // This matches the MT8 OTEL instrumentation which uses "consumer" for all consumer operations
                var operationName = MassTransitConstants.ConsumerOperationName;

                // Don't specify serviceName to use the default tracer service name (like Hangfire)
                scope = tracer.StartActiveInternal(
                    operationName,
                    parent: context.SpanContext,
                    tags: tags,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(MassTransitConstants.IntegrationId);

                // Resource name uses clean destination name: "{clean_destination} {operation}"
                // e.g., "submit-order receive" instead of "loopback://localhost/submit-order receive"
                if (destinationName != null)
                {
                    var cleanDestination = ExtractDestinationName(destinationName);
                    span.ResourceName = $"{cleanDestination} {operation}";
                    // MT8 OTEL: messaging.destination.name = clean name, messaging.masstransit.destination_address = full URI
                    tags.DestinationName = cleanDestination;
                    tags.DestinationAddress = destinationName;
                }
                else if (messageType != null)
                {
                    span.ResourceName = $"{messageType} {operation}";
                    tags.DestinationName = messageType;
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
                }

                // Set message type if provided
                if (messageType != null)
                {
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating MassTransit consumer scope");
            }

            return scope;
        }

        internal static void SetPublishContextTags(MassTransitTags tags, IPublishContext? context)
        {
            if (context == null)
            {
                return;
            }

            tags.MessageId = context.MessageId?.ToString();
            tags.ConversationId = context.ConversationId?.ToString();
            tags.SourceAddress = context.SourceAddress?.ToString();
            tags.DestinationAddress = context.DestinationAddress?.ToString();
            tags.InitiatorId = context.InitiatorId?.ToString();
        }

        internal static void SetConsumeContextTags(MassTransitTags tags, IConsumeContext? context)
        {
            if (context == null)
            {
                return;
            }

            tags.MessageId = context.MessageId?.ToString();
            tags.ConversationId = context.ConversationId?.ToString();
            tags.CorrelationId = context.CorrelationId?.ToString();
            tags.SourceAddress = context.SourceAddress?.ToString();
            tags.DestinationAddress = context.DestinationAddress?.ToString();
            tags.InitiatorId = context.InitiatorId?.ToString();
            tags.RequestId = context.RequestId?.ToString();
            tags.ResponseAddress = context.ResponseAddress?.ToString();
            tags.FaultAddress = context.FaultAddress?.ToString();

            // Extract InputAddress from ReceiveContext (MT8 OTEL: messaging.masstransit.input_address)
            try
            {
                var receiveContextObj = context.ReceiveContext;
                if (receiveContextObj != null && receiveContextObj.TryDuckCast<IReceiveContext>(out var receiveContext))
                {
                    var inputAddress = receiveContext.InputAddress?.ToString();
                    if (!string.IsNullOrEmpty(inputAddress))
                    {
                        tags.InputAddress = inputAddress;

                        // MT8 OTEL sets peer.address as "{MessageType}/{MessageNamespace}" from destination
                        // We'll set it from the destination address for similar behavior
                        var destAddress = context.DestinationAddress?.ToString();
                        if (!string.IsNullOrEmpty(destAddress))
                        {
                            // Extract message type info for peer.address
                            var peerAddress = ExtractPeerAddress(destAddress);
                            if (!string.IsNullOrEmpty(peerAddress))
                            {
                                tags.PeerAddress = peerAddress;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MassTransitIntegration: Failed to extract ReceiveContext properties");
            }

            // Determine messaging system from addresses if not already set properly
            // Don't overwrite if already set to a specific transport (not in-memory default)
            if (string.IsNullOrEmpty(tags.MessagingSystem) || tags.MessagingSystem == "in-memory")
            {
                var messagingSystem = DetermineMessagingSystemFromContext(context);
                if (!string.IsNullOrEmpty(messagingSystem) && messagingSystem != "in-memory")
                {
                    tags.MessagingSystem = messagingSystem;
                }
            }
        }

        /// <summary>
        /// Extracts peer.address from a destination address for MT8 OTEL compatibility.
        /// Format: "{MessageType}/{MessageNamespace}" e.g., "GettingStartedMessage/Samples.MassTransit.Contracts"
        /// </summary>
        internal static string? ExtractPeerAddress(string? destinationAddress)
        {
            if (string.IsNullOrEmpty(destinationAddress))
            {
                return null;
            }

            // Check for URN format: "urn:message:Namespace:MessageType" or embedded in path
            string urn = destinationAddress!;

            // If it's a URI, try to extract the path which may contain the URN
            if (Uri.TryCreate(destinationAddress, UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath.TrimStart('/');
                if (!string.IsNullOrEmpty(path))
                {
                    // Get the last segment which may be the URN
                    var lastSlash = path.LastIndexOf('/');
                    urn = lastSlash >= 0 && lastSlash < path.Length - 1
                        ? path.Substring(lastSlash + 1)
                        : path;
                }
            }

            // Parse URN format: urn:message:Namespace:MessageType
            if (urn.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = urn.Substring("urn:message:".Length).Split(':');
                if (parts.Length >= 2)
                {
                    // Format: MessageType/Namespace (MT8 OTEL style)
                    var messageType = parts[parts.Length - 1];
                    var ns = string.Join(".", parts, 0, parts.Length - 1);
                    return $"{messageType}/{ns}";
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts a clean destination name from a MassTransit address URI.
        /// Matches MT8 OTEL behavior in BaseSendTransportContext.ActivityDestination.
        /// For example: "loopback://localhost/submit-order" -> "submit-order"
        /// Or for message types: "urn:message:Namespace:MessageType" -> "MessageType"
        /// Or for publish URIs: "loopback://localhost/urn:message:Namespace:MessageType" -> "MessageType"
        /// Special endpoints are normalized: "_bus_xxx" -> "bus", "_endpoint_xxx" -> "endpoint"
        /// </summary>
        internal static string ExtractDestinationName(string? fullAddress)
        {
            if (string.IsNullOrEmpty(fullAddress))
            {
                return "unknown";
            }

            // Handle direct URN format (urn:message:Namespace:MessageType)
            if (fullAddress!.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractMessageTypeFromUrn(fullAddress);
            }

            // Try to parse as URI and extract the path
            string entityName = fullAddress;
            try
            {
                if (Uri.TryCreate(fullAddress, UriKind.Absolute, out var uri))
                {
                    // Get the last segment of the path
                    var path = uri.AbsolutePath.TrimStart('/');
                    if (!string.IsNullOrEmpty(path))
                    {
                        // If path contains slashes, get the last segment
                        var lastSlash = path.LastIndexOf('/');
                        if (lastSlash >= 0 && lastSlash < path.Length - 1)
                        {
                            entityName = path.Substring(lastSlash + 1);
                        }
                        else
                        {
                            entityName = path;
                        }
                    }
                }
            }
            catch
            {
                // Continue with fullAddress as entityName
            }

            // Check if the extracted path is itself a URN (e.g., from "loopback://localhost/urn:message:Namespace:MessageType")
            if (entityName.StartsWith("urn:message:", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractMessageTypeFromUrn(entityName);
            }

            // MT8 OTEL-style normalization of special endpoint names
            // See: BaseSendTransportContext.cs in MassTransit source
            if (entityName.IndexOf("_bus_", StringComparison.Ordinal) >= 0)
            {
                return "bus";
            }

            if (entityName.IndexOf("_endpoint_", StringComparison.Ordinal) >= 0)
            {
                return "endpoint";
            }

            if (entityName.IndexOf("_signalr_", StringComparison.Ordinal) >= 0)
            {
                return "signalr";
            }

            if (entityName.StartsWith("Instance_", StringComparison.Ordinal))
            {
                return "instance";
            }

            return entityName;
        }

        /// <summary>
        /// Extracts just the message type name from a URN like "urn:message:Namespace:MessageType" -> "MessageType"
        /// </summary>
        private static string ExtractMessageTypeFromUrn(string urn)
        {
            // Extract just the message type name (last segment after colon)
            var lastColon = urn.LastIndexOf(':');
            if (lastColon > 0 && lastColon < urn.Length - 1)
            {
                return urn.Substring(lastColon + 1);
            }

            return urn;
        }

        internal static string DetermineMessagingSystem(string? destination)
        {
            if (string.IsNullOrEmpty(destination))
            {
                return "in-memory";
            }

            // At this point, destination is not null
            if (destination!.IndexOf("rabbitmq://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "rabbitmq";
            }

            if (destination.IndexOf("sb://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                destination.IndexOf("servicebus", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "azureservicebus";
            }

            if (destination.IndexOf("amazonsqs://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "amazonsqs";
            }

            if (destination.IndexOf("kafka://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "kafka";
            }

            if (destination.IndexOf("loopback://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "in-memory";
            }

            return "in-memory";
        }

        private static string? DetermineMessagingSystemFromContext(IConsumeContext context)
        {
            var sourceAddress = context.SourceAddress?.ToString();
            var destAddress = context.DestinationAddress?.ToString();

            return DetermineMessagingSystem(sourceAddress ?? destAddress);
        }
    }
}
