// <copyright file="MassTransitIntegration.cs" company="Datadog">
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
    internal static class MassTransitIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitIntegration));

        internal static Scope? CreateProducerScope(
            Tracer tracer,
            string operation,
            string? messageType,
            string? destinationName = null,
            DateTimeOffset? startTime = null)
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

                var serviceName = perTraceSettings.Schema.Messaging.GetServiceName(MassTransitConstants.MessagingType);
                var operationName = $"{MassTransitConstants.MessagingType}.{operation}";

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags,
                    serviceName: serviceName,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;

                // Determine messaging system from destination or default to in-memory
                var messagingSystem = DetermineMessagingSystem(destinationName);
                tags.MessagingSystem = messagingSystem;

                // Set resource name
                if (messageType != null)
                {
                    span.ResourceName = $"{operation} {messageType}";
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
                }

                if (destinationName != null)
                {
                    tags.DestinationName = destinationName;
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
            DateTimeOffset? startTime = null)
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

                var serviceName = perTraceSettings.Schema.Messaging.GetServiceName(MassTransitConstants.MessagingType);
                var operationName = $"{MassTransitConstants.MessagingType}.{operation}";

                scope = tracer.StartActiveInternal(
                    operationName,
                    parent: context.SpanContext,
                    tags: tags,
                    serviceName: serviceName,
                    startTime: startTime);

                var span = scope.Span;
                span.Type = SpanTypes.Queue;

                // Default to in-memory for consumer
                tags.MessagingSystem = "in-memory";

                // Set resource name
                if (messageType != null)
                {
                    span.ResourceName = $"{operation} {messageType}";
                    tags.MessageTypes = $"urn:message:{messageType}";
                }
                else
                {
                    span.ResourceName = operation;
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
            tags.SourceAddress = context.SourceAddress?.ToString();
            tags.DestinationAddress = context.DestinationAddress?.ToString();
            tags.InitiatorId = context.InitiatorId?.ToString();
            tags.RequestId = context.RequestId?.ToString();
            tags.ResponseAddress = context.ResponseAddress?.ToString();
            tags.FaultAddress = context.FaultAddress?.ToString();

            // Determine messaging system from addresses
            var messagingSystem = DetermineMessagingSystemFromContext(context);
            if (messagingSystem != null)
            {
                tags.MessagingSystem = messagingSystem;
            }
        }

        private static string DetermineMessagingSystem(string? destination)
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
