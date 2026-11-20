// <copyright file="GooglePubSubCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    internal static class GooglePubSubCommon
    {
        internal const string DefaultProduceEdgeTag = "type:google-pubsub";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(GooglePubSubCommon));

        // The consume checkpoint tag (subscription vs topic) is pending DSM2-747; Java and the merged
        // Python integration both identify the consume edge by subscription, so that is the default here.
        private static readonly EdgeTags DefaultProduceEdgeTags = new(["direction:out", "type:google-pubsub"]);
        private static readonly EdgeTags DefaultConsumeEdgeTags = new(["direction:in", "type:google-pubsub"]);

        // Maps a SubscriptionHandler instance to the subscription id of the client that started it,
        // so the per-message handler sees the subscription without capturing it in the delegate.
        private static readonly ConditionalWeakTable<object, string> HandlerSubscriptions = new();

        internal static void RememberSubscription(object subscriber, object handler)
        {
            try
            {
                var subscriptionId = subscriber.DuckCast<ISubscriberClient>()?.SubscriptionName?.SubscriptionId;
                if (subscriptionId is { Length: > 0 })
                {
#if NETCOREAPP3_1_OR_GREATER
                    HandlerSubscriptions.AddOrUpdate(handler, subscriptionId);
#else
                    HandlerSubscriptions.GetValue(handler, _ => subscriptionId);
#endif
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting the subscription id of a Google Cloud Pub/Sub subscriber");
            }
        }

        internal static string? GetSubscription(object? handler)
        {
            return handler is not null && HandlerSubscriptions.TryGetValue(handler, out var subscriptionId)
                       ? subscriptionId
                       : null;
        }

        internal static string? GetTopic(object publisher)
        {
            try
            {
                return publisher.DuckCast<IPublisherClient>()?.TopicName?.TopicId;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting the topic id of a Google Cloud Pub/Sub publisher");
                return null;
            }
        }

        // NOTE: the tags must be sorted in alphabetical order
        internal static EdgeTags BuildProduceEdgeTags(string? topicId)
        {
            var topic = topicId ?? string.Empty;
            return string.IsNullOrEmpty(topic)
                       ? DefaultProduceEdgeTags
                       : new EdgeTags(["direction:out", $"topic:{topic}", "type:google-pubsub"]);
        }

        // NOTE: the tags must be sorted in alphabetical order
        internal static EdgeTags BuildConsumeEdgeTags(string? subscriptionId)
        {
            var subscription = subscriptionId ?? string.Empty;
            return string.IsNullOrEmpty(subscription)
                       ? DefaultConsumeEdgeTags
                       : new EdgeTags(["direction:in", $"subscription:{subscription}", "type:google-pubsub"]);
        }

        internal static long GetMessageSize<TMessage>(TMessage message)
            where TMessage : IPubsubMessage, IDuckType
        {
            if (message.Instance is null)
            {
                return 0;
            }

            try
            {
                return message.CalculateSize();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calculating the size of a Google Cloud Pub/Sub message");
                return 0;
            }
        }

        internal static double? GetMessageQueueTimeMs(Span span, IPubsubMessage? message)
        {
            try
            {
                if (message?.Instance is not null && message.PublishTime is not null)
                {
                    var consumeTime = span.StartTime.UtcDateTime;
                    var produceTime = message.PublishTime.ToDateTime();
                    if (produceTime > DateTime.MinValue)
                    {
                        return Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading the publish time of a Google Cloud Pub/Sub message");
            }

            return null;
        }

        internal static Scope? CreateProducerSpan(Tracer tracer, string? topicId)
        {
            try
            {
                var settings = tracer.CurrentTraceSettings;
                if (!settings.Settings.IsIntegrationEnabled(IntegrationId.GoogleCloudPubSub))
                {
                    return null;
                }

                var operationName = settings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.GoogleCloudPubSub);
                var (serviceName, serviceNameSource) = settings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.GoogleCloudPubSub);
                var tags = settings.Schema.Messaging.CreateGoogleCloudPubSubTags(SpanKinds.Producer);

                var scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = string.IsNullOrEmpty(topicId) ? "Produce Topic" : $"Produce Topic {topicId}";
                tags.MessagingDestinationName = topicId;
                tags.LegacyMessageBusDestination = topicId;
                span.SetTag(Tags.Measured, "1");

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.GoogleCloudPubSub);
                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating the producer scope.");
                return null;
            }
        }

        internal static Scope? CreateConsumerSpan(Tracer tracer, string? subscriptionId, PropagationContext extractedContext)
        {
            try
            {
                var settings = tracer.CurrentTraceSettings;
                if (!settings.Settings.IsIntegrationEnabled(IntegrationId.GoogleCloudPubSub))
                {
                    return null;
                }

                var operationName = settings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.GoogleCloudPubSub);
                var (serviceName, serviceNameSource) = settings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.GoogleCloudPubSub);
                var tags = settings.Schema.Messaging.CreateGoogleCloudPubSubTags(SpanKinds.Consumer);

                var scope = tracer.StartActiveInternal(operationName, parent: extractedContext.SpanContext, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = string.IsNullOrEmpty(subscriptionId) ? "Consume Subscription" : $"Consume Subscription {subscriptionId}";
                tags.MessagingSourceName = subscriptionId;
                tags.LegacyMessageBusDestination = subscriptionId;
                span.SetTag(Tags.Measured, "1");

                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.GoogleCloudPubSub);
                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating the consumer scope.");
                return null;
            }
        }
    }
}
