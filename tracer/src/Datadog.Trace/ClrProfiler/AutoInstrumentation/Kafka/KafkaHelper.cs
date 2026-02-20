// <copyright file="KafkaHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
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
        private static readonly string[] DefaultProduceEdgeTags = ["direction:out", "type:kafka"];
        private static bool _headersInjectionEnabled = true;

        [ThreadStatic]
        private static bool _isGettingClusterId;

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

                string serviceName = settings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.Kafka);
                KafkaTags tags = settings.Schema.Messaging.CreateKafkaTags(SpanKinds.Producer);

                scope = tracer.StartActiveInternal(
                    operationName,
                    tags: tags,
                    serviceName: serviceName,
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

        private static long GetMessageSize<T>(T message)
            where T : IMessage
        {
            if (((IDuckType)message).Instance is null)
            {
                return 0;
            }

            var size = MessageSizeHelper.TryGetSize(message.Key);
            size += MessageSizeHelper.TryGetSize(message.Value);

            if (message.Headers == null)
            {
                return size;
            }

            for (var i = 0; i < message.Headers.Count; i++)
            {
                var header = message.Headers[i];
                size += Encoding.UTF8.GetByteCount(header.Key);
                var value = header.GetValueBytes();
                if (value != null)
                {
                    size += value.Length;
                }
            }

            return size;
        }

        internal static Scope? CreateConsumerScope(
            Tracer tracer,
            DataStreamsManager dataStreamsManager,
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
                PathwayContext? pathwayContext = null;

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

                    if (dataStreamsManager.IsEnabled)
                    {
                        try
                        {
                            pathwayContext = dataStreamsManager.ExtractPathwayContext(headers);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error extracting PathwayContext from Kafka message");
                        }
                    }
                }

                var serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.Kafka);
                var tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateKafkaTags(SpanKinds.Consumer);

                scope = tracer.StartActiveInternal(operationName, parent: extractedContext.SpanContext, tags: tags, serviceName: serviceName);
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

                var consumerClusterId = string.Empty;
                if (ConsumerCache.TryGetConsumerGroup(consumer, out var groupId, out var bootstrapServers, out var clusterId))
                {
                    tags.ConsumerGroup = groupId;
                    tags.BootstrapServers = bootstrapServers;
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        tags.ClusterId = clusterId;
                        consumerClusterId = clusterId;
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

                if (dataStreamsManager.IsEnabled)
                {
                    // TODO: we could pool these arrays to reduce allocations
                    // NOTE: the tags must be sorted in alphabetical order
                    string[] edgeTags;
                    if (!string.IsNullOrEmpty(consumerClusterId))
                    {
                        edgeTags = string.IsNullOrEmpty(topic)
                                       ? new[] { "direction:in", $"group:{groupId}", $"kafka_cluster_id:{consumerClusterId}", "type:kafka" }
                                       : new[] { "direction:in", $"group:{groupId}", $"kafka_cluster_id:{consumerClusterId}", $"topic:{topic}", "type:kafka" };
                    }
                    else
                    {
                        edgeTags = string.IsNullOrEmpty(topic)
                                       ? new[] { "direction:in", $"group:{groupId}", "type:kafka" }
                                       : new[] { "direction:in", $"group:{groupId}", $"topic:{topic}", "type:kafka" };
                    }

                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        message?.Instance is null || dataStreamsManager.IsInDefaultState ? 0 : GetMessageSize(message),
                        tags.MessageQueueTimeMs == null ? 0 : (long)tags.MessageQueueTimeMs,
                        pathwayContext);

                    message?.Headers?.Remove(DataStreamsPropagationHeaders.TemporaryBase64PathwayContext); // remove eventual junk
                    if (!tracer.CurrentTraceSettings.Settings.KafkaCreateConsumerScopeEnabled && message?.Headers is not null && span.Context.PathwayContext != null)
                    {
                        // write the _new_ pathway (the "consume" checkpoint that we just set above) to the headers as a way to pass its value to an eventual
                        // call to SpanContextExtractor.Extract by a user who'd like to re-pair pathways after a batch consume.
                        // Note that this header only exists on the consume side, and Kafka never sees it.
                        var base64PathwayContext = Convert.ToBase64String(BitConverter.GetBytes(span.Context.PathwayContext.Value.Hash.Value));
                        message.Headers.Add(DataStreamsPropagationHeaders.TemporaryBase64PathwayContext, Encoding.UTF8.GetBytes(base64PathwayContext));
                    }
                }
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
        /// <param name="dataStreamsManager">The global data streams manager</param>
        /// <param name="topic">Topic name</param>
        /// <param name="message">The duck-typed Kafka Message object</param>
        /// <typeparam name="TTopicPartitionMarker">The TopicPartition type (used  optimisation purposes)</typeparam>
        /// <typeparam name="TMessage">The type of the duck-type proxy</typeparam>
        internal static void TryInjectHeaders<TTopicPartitionMarker, TMessage>(
            Span span,
            DataStreamsManager dataStreamsManager,
            string topic,
            TMessage message)
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

                if (dataStreamsManager.IsEnabled)
                {
                    var producerClusterId = string.Empty;
                    if (span.Tags is KafkaTags kafkaTags && !string.IsNullOrEmpty(kafkaTags.ClusterId))
                    {
                        producerClusterId = kafkaTags.ClusterId;
                    }

                    string[] edgeTags;
                    if (!string.IsNullOrEmpty(producerClusterId))
                    {
                        edgeTags = string.IsNullOrEmpty(topic)
                                       ? new[] { "direction:out", $"kafka_cluster_id:{producerClusterId}", "type:kafka" }
                                       : new[] { "direction:out", $"kafka_cluster_id:{producerClusterId}", $"topic:{topic}", "type:kafka" };
                    }
                    else
                    {
                        edgeTags = string.IsNullOrEmpty(topic)
                                       ? DefaultProduceEdgeTags
                                       : new[] { "direction:out", $"topic:{topic}", "type:kafka" };
                    }

                    var msgSize = dataStreamsManager.IsInDefaultState ? 0 : GetMessageSize(message);
                    // produce is always the start of the edge, so defaultEdgeStartMs is always 0
                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, msgSize, 0);
                    // DSM context should NOT be injected state if the message value size is <= DSM header size (~34 bytes).
                    // This is needed to avoid situations when DSM context injection causes a significant
                    // percentage increase in overall message size, leading to capacity issues on the kafka server.
                    if (dataStreamsManager.IsInDefaultState && MessageSizeHelper.TryGetSize(message.Value) <= 34)
                    {
                        return;
                    }

                    dataStreamsManager.InjectPathwayContext(span.Context.PathwayContext, adapter);
                }
            }
            catch (Exception ex)
            {
                // don't keep trying if we run into problems
                _headersInjectionEnabled = false;
                Log.Warning(ex, "There was a problem injecting headers into the Kafka record. Disabling Headers injection");
            }
        }

        internal static string? GetClusterId(string bootstrapServers)
        {
            if (_isGettingClusterId)
            {
                return null;
            }

            if (string.IsNullOrEmpty(bootstrapServers))
            {
                return null;
            }

            try
            {
                _isGettingClusterId = true;

                var configType = Type.GetType("Confluent.Kafka.AdminClientConfig, Confluent.Kafka");
                if (configType is null)
                {
                    return null;
                }

                var config = Activator.CreateInstance(configType);
                if (!config.TryDuckCast<IAdminClientConfig>(out var adminConfig))
                {
                    return null;
                }

                adminConfig.BootstrapServers = bootstrapServers;

                var builderType = Type.GetType("Confluent.Kafka.AdminClientBuilder, Confluent.Kafka");
                if (builderType is null)
                {
                    return null;
                }

                var builder = Activator.CreateInstance(builderType, new object[] { ((IDuckType)adminConfig).Instance! });
                if (!builder.TryDuckCast<IAdminClientBuilder>(out var adminBuilder))
                {
                    return null;
                }

                var adminClientObj = adminBuilder.Build();
                if (adminClientObj is null)
                {
                    return null;
                }

                if (!adminClientObj.TryDuckCast<IAdminClient>(out var adminClient))
                {
                    (adminClientObj as IDisposable)?.Dispose();
                    return null;
                }

                try
                {
                    var duckTask = adminClient.DescribeClusterAsync(null);
                    var describeResult = duckTask.GetAwaiter().GetResult();
                    return describeResult?.ClusterId;
                }
                finally
                {
                    adminClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error extracting cluster_id from Kafka metadata");
                return null;
            }
            finally
            {
                _isGettingClusterId = false;
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
