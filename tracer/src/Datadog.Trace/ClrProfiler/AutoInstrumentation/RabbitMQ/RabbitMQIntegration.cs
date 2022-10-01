// <copyright file="RabbitMQIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// Tracing integration for RabbitMQ.Client
    /// </summary>
    internal static class RabbitMQIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.RabbitMQ);

        private const string OperationName = "amqp.command";
        private const string ServiceName = "rabbitmq";

        internal const IntegrationId IntegrationId = Configuration.IntegrationId.RabbitMQ;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RabbitMQIntegration));

        internal static Scope CreateScope(Tracer tracer, out RabbitMQTags tags, string command, string spanKind, ISpanContext parentContext = null, DateTimeOffset? startTime = null, string queue = null, string exchange = null, string routingKey = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                tags = new RabbitMQTags(spanKind);
                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveInternal(OperationName, parent: parentContext, tags: tags, serviceName: serviceName, startTime: startTime);
                var span = scope.Span;

                span.Type = SpanTypes.Queue;
                span.ResourceName = command;
                tags.Command = command;

                tags.Queue = queue;
                tags.Exchange = exchange;
                tags.RoutingKey = routingKey;

                tags.InstrumentationName = IntegrationName;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
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

        internal static CallTargetState BasicDeliver_OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, TBasicProperties basicProperties, TBody body)
            where TBasicProperties : IBasicProperties
            where TBody : IBody // ReadOnlyMemory<byte> body in 6.0.0
        {
            if (IsActiveScopeRabbitMQ(Tracer.Instance))
            {
                // we are already instrumenting this,
                // don't instrument nested methods that belong to the same stacktrace
                // e.g. DerivedType.HandleBasicDeliver -> BaseType.RabbitMQ.Client.IAsyncBasicConsumer.HandleBasicDeliver
                return CallTargetState.GetDefault();
            }

            SpanContext propagatedContext = null;

            // try to extract propagated context values from headers
            if (basicProperties?.Headers != null)
            {
                try
                {
                    propagatedContext = SpanContextPropagator.Instance.Extract(basicProperties.Headers, default(ContextPropagation));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated headers.");
                }
            }

            var scope = RabbitMQIntegration.CreateScope(Tracer.Instance, out RabbitMQTags tags, "basic.deliver", parentContext: propagatedContext, spanKind: SpanKinds.Consumer, exchange: exchange, routingKey: routingKey);
            if (tags != null)
            {
                tags.MessageSize = body?.Length.ToString() ?? "0";
            }

            return new CallTargetState(scope);
        }

        internal static bool IsActiveScopeRabbitMQ(Tracer tracer)
        {
            var scope = tracer.InternalActiveScope;
            var parent = scope?.Span;

            return parent != null && parent.OperationName == OperationName;
        }
    }
}
