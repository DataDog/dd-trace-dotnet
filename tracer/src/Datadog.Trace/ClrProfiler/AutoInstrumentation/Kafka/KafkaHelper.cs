// <copyright file="KafkaHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Console = System.Console;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal static class KafkaHelper
    {
        internal const string GroupIdKey = "group.id";
        internal const string BootstrapServersKey = "bootstrap.servers";
        internal const string EnableDeliveryReportsField = "dotnet.producer.enable.delivery.reports";
        private const string MessagingType = "kafka";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(KafkaHelper));
        private static readonly string[] DefaultProduceEdgeTags = ["direction:out", "type:kafka"];
        private static bool _headersInjectionEnabled = true;

        // Thread-local flag to prevent infinite recursion when AdminClient creates internal Producer
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
                var settings = tracer.Settings;
                if (!settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                string operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingType);
                if (parent is not null &&
                    parent.OperationName == operationName &&
                    parent.GetTag(Tags.InstrumentationName) != null)
                {
                    return null;
                }

                string serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingType);
                KafkaTags tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateKafkaTags(SpanKinds.Producer);

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
                        Log.Information("ROBC: Added cluster_id tag to Kafka producer span: {ClusterId}", clusterId);
                        Console.WriteLine($"ROBC: Added cluster_id tag to Kafka producer span: {clusterId}");
                    }
                    else
                    {
                        Log.Information("ROBC: No cluster_id available for Kafka producer span");
                        Console.WriteLine("ROBC: No cluster_id available for Kafka producer span");
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
                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, settings, enabledWithGlobalSetting: false);
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
                if (!tracer.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId))
                {
                    // integration disabled, don't create a scope/span, skip this trace
                    return null;
                }

                var parent = tracer.ActiveScope?.Span;
                string operationName = tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingType);
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

                var serviceName = tracer.CurrentTraceSettings.Schema.Messaging.GetServiceName(MessagingType);
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

                if (ConsumerCache.TryGetConsumerGroup(consumer, out var groupId, out var bootstrapServers, out var clusterId))
                {
                    tags.ConsumerGroup = groupId;
                    tags.BootstrapServers = bootstrapServers;
                    if (!string.IsNullOrEmpty(clusterId))
                    {
                        tags.ClusterId = clusterId;
                        Log.Information("ROBC: Added cluster_id tag to Kafka consumer span: {ClusterId}", clusterId);
                        Console.WriteLine($"ROBC: Added cluster_id tag to Kafka consumer span: {clusterId}");
                    }
                    else
                    {
                        Log.Information("ROBC: No cluster_id available for Kafka consumer span");
                        Console.WriteLine("ROBC: No cluster_id available for Kafka consumer span");
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

                tags.SetAnalyticsSampleRate(KafkaConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);

                if (dataStreamsManager.IsEnabled)
                {
                    // TODO: we could pool these arrays to reduce allocations
                    // NOTE: the tags must be sorted in alphabetical order
                    var edgeTags = string.IsNullOrEmpty(topic)
                                       ? new[] { "direction:in", $"group:{groupId}", "type:kafka" }
                                       : new[] { "direction:in", $"group:{groupId}", $"topic:{topic}", "type:kafka" };

                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        message?.Instance is null || dataStreamsManager.IsInDefaultState ? 0 : GetMessageSize(message),
                        tags.MessageQueueTimeMs == null ? 0 : (long)tags.MessageQueueTimeMs,
                        pathwayContext);

                    message?.Headers?.Remove(DataStreamsPropagationHeaders.TemporaryBase64PathwayContext); // remove eventual junk
                    if (!tracer.Settings.KafkaCreateConsumerScopeEnabled && message?.Headers is not null && span.Context.PathwayContext != null)
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
                if (!tracer.Settings.IsIntegrationEnabled(KafkaConstants.IntegrationId)
                 || !tracer.Settings.KafkaCreateConsumerScopeEnabled)
                {
                    // integration disabled, skip this trace
                    return;
                }

                var activeScope = tracer.InternalActiveScope;
                var currentSpan = activeScope?.Span;
                if (currentSpan?.OperationName != tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingType))
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
                    var edgeTags = string.IsNullOrEmpty(topic)
                                       ? DefaultProduceEdgeTags
                                       : ["direction:out", $"topic:{topic}", "type:kafka"];
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
            // Prevent re-entrancy: AdminClient internally creates a Producer, which would trigger our instrumentation again
            if (_isGettingClusterId)
            {
                Log.Information("ROBC: Skipping cluster_id retrieval to prevent re-entrancy (AdminClient internal Producer creation)");
                Console.WriteLine("ROBC: Skipping cluster_id retrieval to prevent re-entrancy (AdminClient internal Producer creation)");
                return null;
            }

            if (string.IsNullOrEmpty(bootstrapServers))
            {
                Log.Information("ROBC: Cannot retrieve cluster_id - bootstrap servers is null or empty");
                Console.WriteLine("ROBC: Cannot retrieve cluster_id - bootstrap servers is null or empty");
                return null;
            }

            try
            {
                _isGettingClusterId = true;
                Log.Information("ROBC: Attempting to retrieve cluster_id from Kafka using bootstrap servers: {BootstrapServers}", bootstrapServers);
                Console.WriteLine($"ROBC: Attempting to retrieve cluster_id from Kafka using bootstrap servers: {bootstrapServers}");

                // Create AdminClientConfig
                var configType = Type.GetType("Confluent.Kafka.AdminClientConfig, Confluent.Kafka");
                if (configType == null)
                {
                    Log.Information("ROBC: Unable to find Confluent.Kafka.AdminClientConfig type");
                    Console.WriteLine("ROBC: Unable to find Confluent.Kafka.AdminClientConfig type");
                    return null;
                }

                // TODO(FIX!) Type names for Confluent.Kafka are fixed/hardcoded
                var config = Activator.CreateInstance(configType);

                if (config == null || !config.TryDuckCast<IAdminClientConfig>(out var adminConfig))
                {
                    Log.Information("ROBC: Unable to create or duck-cast AdminClientConfig");
                    Console.WriteLine("ROBC: Unable to create or duck-cast AdminClientConfig");
                    return null;
                }

                adminConfig.BootstrapServers = bootstrapServers;

                // Create AdminClientBuilder
                var builderType = Type.GetType("Confluent.Kafka.AdminClientBuilder, Confluent.Kafka");
                if (builderType == null)
                {
                    Log.Information("ROBC: Unable to find Confluent.Kafka.AdminClientBuilder type");
                    Console.WriteLine("ROBC: Unable to find Confluent.Kafka.AdminClientBuilder type");
                    return null;
                }

                // TODO(FIX!) Type names for Confluent.Kafka are fixed/hardcoded
                var builder = Activator.CreateInstance(builderType, config);
                if (builder == null || !builder.TryDuckCast<IAdminClientBuilder>(out var adminBuilder))
                {
                    Log.Information("ROBC: Unable to create or duck-cast AdminClientBuilder");
                    Console.WriteLine("ROBC: Unable to create or duck-cast AdminClientBuilder");
                    return null;
                }

                // Build and use AdminClient
                using (var adminClient = adminBuilder.Build())
                {
                    Log.Information("ROBC: Successfully built AdminClient");
                    Console.WriteLine("ROBC: Successfully built AdminClient");

                    Type? staticType = Type.GetType("Confluent.Kafka.IAdminClientExtensions, Confluent.Kafka");
                    if (staticType == null)
                    {
                        Log.Information("ROBC: Unable to find Confluent.Kafka.IAdminClientExtensions type");
                        Console.WriteLine("ROBC: Unable to find Confluent.Kafka.IAdminClientExtensions type");
                        return null;
                    }

                    Type proxyType = typeof(IAdminClientExtensions); // The type of our proxy
                    if (proxyType == null)
                    {
                        Log.Information("ROC94: Unable to find IAdminClientExtensions type");
                        Console.WriteLine("ROC94: Unable to find IAdminClientExtensions type");
                        return null;
                    }

                    DuckType.CreateTypeResult proxyResult = DuckType.GetOrCreateProxyType(proxyType, staticType);
                    IAdminClientExtensions? proxy = null;
                    if (proxyResult.Success)
                    {
                        // Pass in null, as there's no "instance" to duck type here, to create an instance of our proxy
                        proxy = (IAdminClientExtensions)proxyResult.CreateInstance(null!);
                        Log.Information("ROBC: Successfully created IAdminClientExtensions proxy");
                        Console.WriteLine("ROBC: Successfully created IAdminClientExtensions proxy");
                    }
                    else
                    {
                        Log.Information("ROBC: Unable to create or duck-cast IAdminClientExtensions");
                        Console.WriteLine("ROBC: Unable to create or duck-cast IAdminClientExtensions");
                        return null;
                    }

                    var describeClusterAsync = proxy.DescribeClusterAsync(adminClient, null);

                    // Use a short timeout to avoid blocking
                    var timeout = TimeSpan.FromMilliseconds(100);
                    using var cts = new CancellationTokenSource(timeout);

                    // Wait synchronously with timeout (we're in a constructor, can't be async)
                    if (describeClusterAsync.Wait(timeout))
                    {
                        // Get the Result property from Task<DescribeClusterResult>
                        var resultProperty = describeClusterAsync.GetType().GetProperty("Result");
                        var result = resultProperty?.GetValue(describeClusterAsync);

                        if (result != null && result.TryDuckCast<IDescribeClusterResult>(out var clusterResult))
                        {
                            if (clusterResult?.ClusterId != null)
                            {
                                Log.Information("ROBC: Successfully retrieved cluster_id from Kafka: {ClusterId}", clusterResult.ClusterId);
                                Console.WriteLine($"ROBC: Successfully retrieved cluster_id from Kafka: {clusterResult.ClusterId}");
                                return clusterResult.ClusterId;
                            }
                        }

                        Log.Information("ROBC: DescribeClusterAsync returned null or empty cluster_id");
                        Console.WriteLine("ROBC: DescribeClusterAsync returned null or empty cluster_id");
                    }
                    else
                    {
                        Log.Information("ROBC: DescribeClusterAsync timed out after {TimeoutMs}ms", timeout.TotalMilliseconds);
                        Console.WriteLine($"ROBC: DescribeClusterAsync timed out after {timeout.TotalMilliseconds}ms");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ROBC: Error extracting cluster_id from Kafka metadata");
                Console.WriteLine($"ROBC: Error extracting cluster_id from Kafka metadata: {ex}");
            }
            finally
            {
                _isGettingClusterId = false;
            }

            return null;
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
