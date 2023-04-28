// <copyright file="KafkaHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class KafkaHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(KafkaHelper));
        private static bool _headersInjectionEnabled = true;
        private static string[] defaultProduceEdgeTags = new[] { "direction:out", "type:kafka" };

        internal static Scope CreateProducerScope(Tracer tracer, ITopicPartition topicPartition, bool isTombstone, bool finishOnClose)
        {
            Scope scope = null;

            try
            {
                var settings = tracer.Settings;
                if (!settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                if (parent is not null &&
                    parent.OperationName == KafkaConstants.ProduceOperationName &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    return null;
                }

                string serviceName = settings.GetServiceName(tracer, KafkaConstants.ServiceName);
                var tags = new KafkaTags(SpanKinds.Producer);

                scope = tracer.StartActiveInternal(
                    KafkaConstants.ProduceOperationName,
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

                if (isTombstone)
                {
                    tags.Tombstone = "true";
                }

                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(KafkaConstants.IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        internal static Scope CreateConsumerScope(
            Tracer tracer,
            DataStreamsManager dataStreamsManager,
            object consumer,
            string topic,
            Partition? partition,
            Offset? offset,
            IMessage message)
        {
            Scope scope = null;

            try
            {
                if (!tracer.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                if (parent is not null &&
                    parent.OperationName == KafkaConstants.ConsumeOperationName &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    return null;
                }

                SpanContext propagatedContext = null;
                PathwayContext? pathwayContext = null;

                // Try to extract propagated context from headers
                if (message?.Headers is not null)
                {
                    var headers = new KafkaHeadersCollectionAdapter(message.Headers);

                    try
                    {
                        propagatedContext = SpanContextPropagator.Instance.Extract(headers);
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

                string serviceName = tracer.Settings.GetServiceName(tracer, KafkaConstants.ServiceName);

                var tags = new KafkaTags(SpanKinds.Consumer);

                scope = tracer.StartActiveInternal(KafkaConstants.ConsumeOperationName, parent: propagatedContext, tags: tags, serviceName: serviceName);

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

                if (ConsumerGroupHelper.TryGetConsumerGroup(consumer, out var groupId))
                {
                    tags.ConsumerGroup = groupId;
                }

                if (message is not null && message.Timestamp.Type != 0)
                {
                    var consumeTime = span.StartTime.UtcDateTime;
                    var produceTime = message.Timestamp.UtcDateTime;
                    tags.MessageQueueTimeMs = Math.Max(0, (consumeTime - produceTime).TotalMilliseconds);
                }

                if (message is not null && message.Value is null)
                {
                    tags.Tombstone = "true";
                }

                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);

                if (dataStreamsManager.IsEnabled)
                {
                    // remove any leftover junk they may be left in the headers
                    message?.Headers?.Remove(DataStreamsPropagationHeaders.TemporaryBase64PathwayContext);
                    message?.Headers?.Remove(DataStreamsPropagationHeaders.TemporaryEdgeTags);

                    if (!tracer.Settings.KafkaCreateConsumerScopeEnabled && message?.Headers is not null)
                    {
                        // This is a brilliant and horrible approach to let customers who are already
                        // extracting the span context from a Kafka message automatically get
                        // checkpointing support in their custom instrumentation
                        if (message.Headers.TryGetLastBytes(DataStreamsPropagationHeaders.PropagationKey, out var bytes))
                        {
                            // annoyingly we have to re-encode the pathwayContext as base64 so we can read it as
                            // a string in SpanContextExtractor.Extract
                            // if there was no pathway context, we don't need to encode it
                            var base64PathwayContext = System.Convert.ToBase64String(bytes);
                            message.Headers.Add(DataStreamsPropagationHeaders.TemporaryBase64PathwayContext, Encoding.UTF8.GetBytes(base64PathwayContext));
                        }

                        // ','is not a valid character in kafka topic or group names, so we use as the
                        // separator here NOTE: the tags must be sorted in alphabetical order
                        var edgeTags = string.IsNullOrEmpty(topic)
                                           ? $"direction:in,group:{groupId},type:kafka"
                                           : $"direction:in,group:{groupId},topic:{topic},type:kafka";
                        message.Headers.Add(DataStreamsPropagationHeaders.TemporaryEdgeTags, Encoding.UTF8.GetBytes(edgeTags));
                    }
                    else
                    {
                        span.Context.MergePathwayContext(pathwayContext);

                        // TODO: we could pool these arrays to reduce allocations
                        // NOTE: the tags must be sorted in alphabetical order
                        var edgeTags = string.IsNullOrEmpty(topic)
                                           ? new[] { "direction:in", $"group:{groupId}", "type:kafka" }
                                           : new[] { "direction:in", $"group:{groupId}", $"topic:{topic}", "type:kafka" };

                        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags);
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
                if (!tracer.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId)
                    || !tracer.Settings.KafkaCreateConsumerScopeEnabled)
                {
                    // integration disabled, skip this trace
                    return;
                }

                var activeScope = tracer.InternalActiveScope;
                var currentSpan = activeScope?.Span;
                if (currentSpan?.OperationName != KafkaConstants.ConsumeOperationName)
                {
                    // Not currently in a consumer operation
                    return;
                }

                // TODO: record end-to-end time?
                activeScope.Dispose();
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
            if (!_headersInjectionEnabled)
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

                SpanContextPropagator.Instance.Inject(span.Context, adapter);

                if (dataStreamsManager.IsEnabled)
                {
                    var edgeTags = string.IsNullOrEmpty(topic)
                        ? defaultProduceEdgeTags
                        : new[] { "direction:out", $"topic:{topic}", "type:kafka" };
                    span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags);
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
    }
}
