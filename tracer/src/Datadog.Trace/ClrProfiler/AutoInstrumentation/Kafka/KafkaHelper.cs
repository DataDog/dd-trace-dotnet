// <copyright file="KafkaHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class KafkaHelper
    {
        internal const string GroupIdKey = "group.id";
        internal const string BootstrapServersKey = "bootstrap.servers";
        internal const string EnableDeliveryReportsField = "dotnet.producer.enable.delivery.reports";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(KafkaHelper));
        private static readonly ConcurrentDictionary<string, string?> ClusterIdCache = new();
        private static bool _headersInjectionEnabled = true;

        internal static Scope? CreateProducerScope(
            Tracer tracer,
            object producer,
            ITopicPartition topicPartition,
            bool isTombstone,
            bool finishOnClose)
        {
            Scope? scope = null;

            try
            {
                var settings = tracer.CurrentTraceSettings;
                if (!settings.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                string operationName = settings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.Kafka);
                if (parent is not null &&
                    parent.OperationName == operationName &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    return null;
                }

                var (serviceName, serviceNameSource) = settings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.Kafka);
                KafkaTags tags = settings.Schema.Messaging.CreateKafkaTags(SpanKinds.Producer);

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags,
                    serviceName: serviceName,
                    serviceNameSource: serviceNameSource,
                    finishOnClose: finishOnClose);

                string resourceName = $"Produce Topic {(string.IsNullOrEmpty(topicPartition?.Topic) ? "kafka" : topicPartition?.Topic)}";

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = resourceName;
                if (topicPartition?.Partition is not null && !topicPartition.Partition.IsSpecial)
                {
                    tags.Partition = (topicPartition?.Partition).ToString();
                }

                if (ProducerCache.TryGetProducer(producer, out var bootstrapServers, out var clusterId))
                {
                    tags.BootstrapServers = bootstrapServers;
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        tags.ClusterId = clusterId;
                    }
                }

                if (isTombstone)
                {
                    tags.Tombstone = "true";
                }

                if (topicPartition is not null && !string.IsNullOrEmpty(topicPartition.Topic))
                {
                    tags.Topic = topicPartition.Topic;
                }

                // Producer spans should always be measured
                span.SetMetric(Trace.Tags.Measured, 1.0);

                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, settings.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(KafkaConstants.IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope? CreateConsumerScope(
            Tracer tracer,
            object consumer,
            string topic,
            Partition? partition,
            Offset? offset,
            IMessage message)
        {
            Scope? scope = null;

            try
            {
                if (!tracer.CurrentTraceSettings.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                string operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.Kafka);
                if (parent is not null &&
                    parent.OperationName == operationName &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    return null;
                }

                PropagationContext extractedContext = default;

                // Try to extract propagated context from headers
                if (message?.Headers is not null)
                {
                    var headers = new KafkaHeadersCollectionAdapter(message.Headers);

                    try
                    {
                        extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers).MergeBaggageInto(Baggage.Current);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated headers from Kafka message");
                    }
                }

                var (serviceName, serviceNameSource) = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceNameMetadata(MessagingSchema.ServiceType.Kafka);
                var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateKafkaTags(SpanKinds.Consumer);

                scope = tracer.StartActiveInternal(operationName, parent: extractedContext.SpanContext, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(KafkaConstants.IntegrationId);

                string resourceName = $"Consume Topic {(string.IsNullOrEmpty(topic) ? "kafka" : topic)}";

                var span = scope.Span;
                span.Type = SpanTypes.Queue;
                span.ResourceName = resourceName;

                if (partition is not null)
                {
                    tags.Partition = partition.ToString();
                }

                if (offset is not null)
                {
                    tags.Offset = offset.ToString();
                }

                if (ConsumerCache.TryGetConsumerGroup(consumer, out var groupId, out var bootstrapServers, out var consumerClusterId))
                {
                    tags.ConsumerGroup = groupId;
                    tags.BootstrapServers = bootstrapServers;
                    if (!StringUtil.IsNullOrEmpty(consumerClusterId))
                    {
                        tags.ClusterId = consumerClusterId;
                    }
                }

                if (message?.Instance is not null && message.Timestamp.Type != 0)
                {
                    var consumeTime = span.StartTime.UtcDateTime;
                    var produceTime = message.Timestamp.UtcDateTime;
                    tags.MessageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                }

                if (message?.Instance is not null && message.Value is null)
                {
                    tags.Tombstone = "true";
                }

                if (!string.IsNullOrEmpty(topic))
                {
                    tags.Topic = topic;
                }

                // Consumer spans should always be measured
                span.SetTag(Tags.Measured, "1");

                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, tracer.CurrentTraceSettings.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static void CloseConsumerScope(Tracer tracer)
        {
            try
            {
                var settings = tracer.CurrentTraceSettings.Settings;
                if (!settings.IsIntegrationEnabled(KafkaConstants.IntegrationId)
                 || !settings.KafkaCreateConsumerScopeEnabled)
                {
                    // integration disabled, skip this trace
                    return;
                }

                var activeScope = tracer.InternalActiveScope;
                var currentSpan = activeScope?.Span;
                if (currentSpan?.OperationName != tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.Kafka))
                {
                    // Not currently in a consumer operation
                    return;
                }

                // TODO: record end-to-end time?
                activeScope!.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing Kafka consumer scope");
            }
        }

        /// <summary>
        /// Try to inject the prop
        /// </summary>
        /// <param name="span">Current span</param>
        /// <param name="topic">Topic name</param>
        /// <param name="message">The duck-typed Kafka Message object</param>
        /// <param name="producer">The Kafka producer instance, used to look up cluster_id from the cache</param>
        /// <typeparam name="TTopicPartitionMarker">The TopicPartition type (used  optimisation purposes)</typeparam>
        /// <typeparam name="TMessage">The type of the duck-type proxy</typeparam>
        internal static void TryInjectHeaders<TTopicPartitionMarker, TMessage>(
            Span span,
            string topic,
            TMessage message,
            object producer)
            where TMessage : IMessage
        {
            if (!_headersInjectionEnabled || message.Instance is null)
            {
                return;
            }

            try
            {
                if (message.Headers is null)
                {
                    message.Headers = CachedMessageHeadersHelper<TTopicPartitionMarker>.CreateHeaders();
                }

                var adapter = new KafkaHeadersCollectionAdapter(message.Headers);

                var context = new PropagationContext(span.Context, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(context, adapter);
            }
            catch (Exception ex)
            {
                // don't keep trying if we run into problems
                _headersInjectionEnabled = false;
                Log.Warning(ex, "There was a problem injecting headers into the Kafka record. Disabling Headers injection");
            }
        }

        internal static string? GetClusterId(string? bootstrapServers, object clientInstance)
        {
            if (StringUtil.IsNullOrEmpty(bootstrapServers))
            {
                return null;
            }

            if (ClusterIdCache.TryGetValue(bootstrapServers, out var cached))
            {
                return cached;
            }

            try
            {
                var kafkaAssembly = clientInstance.GetType().Assembly;

                // DescribeClusterAsync and DescribeClusterOptions were added in Confluent.Kafka 2.3.0
                var describeClusterOptionsType = kafkaAssembly.GetType("Confluent.Kafka.Admin.DescribeClusterOptions");
                if (describeClusterOptionsType is null)
                {
                    Log.Debug("Confluent.Kafka.Admin.DescribeClusterOptions not found; cluster_id tag requires Confluent.Kafka >= 2.3.0");
                    ClusterIdCache.TryAdd(bootstrapServers, string.Empty);
                    return string.Empty;
                }

                var builderType = kafkaAssembly.GetType("Confluent.Kafka.DependentAdminClientBuilder");
                if (builderType is null)
                {
                    Log.Error("Failed to find Confluent.Kafka.DependentAdminClientBuilder in the Confluent.Kafka assembly");
                    ClusterIdCache.TryAdd(bootstrapServers, string.Empty);
                    return string.Empty;
                }

                var clientHandle = clientInstance.DuckCast<IClientHandle>();
                var handle = clientHandle.Handle;
                if (handle is null)
                {
                    Log.Error("Kafka client handle is null; unable to extract cluster_id");
                    return null;
                }

                var builder = Activator.CreateInstance(builderType, handle)!;
                using var adminClient = builder.DuckCast<IAdminClientBuilder>().Build();
                var clusterId = DescribeClusterWithTimeout(adminClient, describeClusterOptionsType);
                ClusterIdCache.TryAdd(bootstrapServers, clusterId);
                return clusterId;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error extracting cluster_id from Kafka metadata");
                return null;
            }
        }

        private static string? DescribeClusterWithTimeout(IAdminClient adminClient, Type describeClusterOptionsType)
        {
            var options = Activator.CreateInstance(describeClusterOptionsType)!;
            options.DuckCast<IDescribeClusterOptions>().RequestTimeout = TimeSpan.FromSeconds(2);

            var duckTask = adminClient.DescribeClusterAsync(options);

            var originalContext = SynchronizationContext.Current;
            try
            {
                // Set the synchronization context to null to avoid deadlocks.
                SynchronizationContext.SetSynchronizationContext(null);

                // Wait synchronously for the task to complete.
                return duckTask.GetAwaiter().GetResult()?.ClusterId;
            }
            finally
            {
                // Restore the original synchronization context.
                SynchronizationContext.SetSynchronizationContext(originalContext);
            }
        }

        internal static void DisableHeadersIfUnsupportedBroker(Exception exception)
        {
            if (_headersInjectionEnabled && exception is not null && exception.Message.IndexOf("Unknown broker error", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // If we get this exception, it likely means that the message format being used is pre-0.11
                // We do not retry the failed message, we think this will have unnecessary complexity due to the likely rarity of this error
                // We do not selectively disable headers injection, we disable it globally due to the likely rarity of this error
                _headersInjectionEnabled = false;

                Log.Error(exception, "Kafka Broker responded with UNKNOWN_SERVER_ERROR (-1). Please look at broker logs for more information. Tracer message header injection for Kafka is disabled.");
            }
        }
    }
}
