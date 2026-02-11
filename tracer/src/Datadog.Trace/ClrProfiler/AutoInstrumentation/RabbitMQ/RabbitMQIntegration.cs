// <copyright file="RabbitMQIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Utils;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// Tracing integration for RabbitMQ.Client
    /// </summary>
    internal static class RabbitMQIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.RabbitMQ);
        internal const int DefaultMaxMessageSize = 128 * 1024;

        internal const IntegrationId IntegrationId = Configuration.IntegrationId.RabbitMQ;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RabbitMQIntegration));

        internal static Scope? CreateScope(
            Tracer tracer,
            out RabbitMQTags? tags,
            string command,
            string spanKind,
            string? host = null,
            PropagationContext context = default,
            DateTimeOffset? startTime = null,
            string? queue = null,
            string? exchange = null,
            string? routingKey = null)
        {
            tags = null;

            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                tags = perTraceSettings.Schema.Messaging.CreateRabbitMqTags(spanKind);
                var serviceName = perTraceSettings.Schema.Messaging.GetServiceName(MessagingSchema.ServiceType.RabbitMq);
                var operation = GetOperationName(tracer, spanKind);

                scope = tracer.StartActiveInternal(
                    operation,
                    parent: context.SpanContext,
                    tags: tags,
                    serviceName: serviceName,
                    startTime: startTime);

                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = command;
                tags.Command = command;

                tags.Queue = queue;
                tags.Exchange = exchange;
                tags.RoutingKey = routingKey;

                tags.OutHost = host;

                tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        [TestingAndPrivateOnly]
        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
            if (tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0)
            {
                return RabbitMQConstants.AmqpCommand;
            }

            return spanKind switch
            {
                SpanKinds.Producer => tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.Amqp),
                SpanKinds.Consumer => tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.Amqp),
                _ => RabbitMQConstants.AmqpCommand
            };
        }

        internal static long GetHeadersSize(IDictionary<string, object>? headers)
        {
            if (headers == null)
            {
                return 0;
            }

            long size = 0;
            foreach (var pair in headers)
            {
                size += Encoding.UTF8.GetByteCount(pair.Key);
                size += MessageSizeHelper.TryGetSize(pair.Value);
            }

            return size;
        }

        internal static void SetDataStreamsCheckpointOnProduce(Tracer tracer, Span span, RabbitMQTags tags, IDictionary<string, object>? headers, int messageSize)
        {
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
            if (dataStreamsManager == null || headers == null || !dataStreamsManager.IsEnabled)
            {
                return;
            }

            try
            {
                var headersAdapter = new RabbitMQHeadersCollectionAdapter(headers);
                var edgeTags = string.IsNullOrEmpty(tags.Exchange)
                                   ?
                                   // exchange can be empty for "direct"
                                   new[] { "direction:out", $"topic:{tags.Queue ?? tags.RoutingKey}", "type:rabbitmq" }
                                   : new[] { "direction:out", $"exchange:{tags.Exchange}", string.IsNullOrEmpty(tags.RoutingKey) ? "has_routing_key:false" : "has_routing_key:true", "type:rabbitmq" };
                var size = dataStreamsManager.IsInDefaultState ? messageSize : GetHeadersSize(headers) + messageSize;
                span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, size, 0);
                // DSM context will not be injected in default state if its size exceeds 128kb
                if (dataStreamsManager.IsInDefaultState && size > DefaultMaxMessageSize)
                {
                    return;
                }

                dataStreamsManager.InjectPathwayContext(span.Context.PathwayContext, headersAdapter);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set data streams checkpoint on produce");
            }
        }

        internal static void SetDataStreamsCheckpointOnConsume(Tracer tracer, Span span, RabbitMQTags tags, IDictionary<string, object>? headers, int messageSize, long messageTimestamp)
        {
            var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
            if (dataStreamsManager == null || headers == null || !dataStreamsManager.IsEnabled)
            {
                return;
            }

            try
            {
                var headersAdapter = new RabbitMQHeadersCollectionAdapter(headers);
                var edgeTags = new[] { "direction:in", $"topic:{tags.Queue ?? tags.RoutingKey}", "type:rabbitmq" };
                var pathwayContext = dataStreamsManager.ExtractPathwayContext(headersAdapter);
                span.SetDataStreamsCheckpoint(
                    dataStreamsManager,
                    CheckpointKind.Consume,
                    edgeTags,
                    GetHeadersSize(headers) + messageSize,
                    messageTimestamp,
                    pathwayContext);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set data streams checkpoint on consume");
            }
        }

        internal static CallTargetState BasicDeliver_OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, bool redelivered, string? exchange, string? routingKey, TBasicProperties basicProperties, TBody body)
            where TBasicProperties : IReadOnlyBasicProperties
            where TBody : IBody, IDuckType // ReadOnlyMemory<byte> body in 6.0.0
        {
            var tracer = Tracer.Instance;

            if (IsActiveScopeRabbitMqConsume(tracer))
            {
                // we are already instrumenting this,
                // don't instrument nested methods that belong to the same stacktrace
                // e.g. DerivedType.HandleBasicDeliver -> BaseType.RabbitMQ.Client.IAsyncBasicConsumer.HandleBasicDeliver
                return CallTargetState.GetDefault();
            }

            string? queue = null;

            // instance can't be null because these are called from instance methods
            if (QueueHelper.TryGetQueue(instance!, out var queueInner))
            {
                queue = queueInner;
            }

            PropagationContext extractedContext = default;

            // try to extract propagated context values from headers
            if (basicProperties?.Headers != null)
            {
                try
                {
                    extractedContext = tracer.TracerManager.SpanContextPropagator
                                             .Extract(basicProperties.Headers, default(ContextPropagation))
                                             .MergeBaggageInto(Baggage.Current);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated headers.");
                }
            }

            var scope = CreateScope(
                tracer,
                out var tags,
                "basic.deliver",
                context: extractedContext,
                spanKind: SpanKinds.Consumer,
                queue: queue,
                exchange: exchange,
                routingKey: routingKey);

            if (scope is not null && tags != null)
            {
                if (body.Instance is not null)
                {
                    tags.MessageSize = body.Length.ToString() ?? "0";
                }

                var timeInQueue = basicProperties != null && basicProperties.Timestamp.UnixTime != 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - basicProperties.Timestamp.UnixTime : 0;
                SetDataStreamsCheckpointOnConsume(
                    Tracer.Instance,
                    scope.Span,
                    tags,
                    basicProperties?.Headers,
                    body?.Length ?? 0,
                    timeInQueue);
            }

            return new CallTargetState(scope);
        }

        internal static bool IsRabbitMqSpan(Tracer tracer, [NotNullWhen(true)] ISpan? span, string spanKind)
            => span != null && span.OperationName == GetOperationName(tracer, spanKind);

        private static bool IsActiveScopeRabbitMqConsume(Tracer tracer)
            => IsRabbitMqSpan(tracer, tracer.InternalActiveScope?.Span, SpanKinds.Consumer);
    }
}
