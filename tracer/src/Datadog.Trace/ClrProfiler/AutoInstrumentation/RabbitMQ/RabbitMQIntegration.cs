// <copyright file="RabbitMQIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
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

            if (tracer is null || tracer.Settings.IsIntegrationEnabled(IntegrationId) is false)
            {
                // integration disabled or Tracer.Instance is null, don't create a scope, skip this trace
                Log.Debug(tracer is null ? "Tracer.Instance is null." : "RabbitMQ Integration is disabled.");
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

        /********************
         * Duck Typing Types
         */
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1600 // Elements must be documented
        [DuckCopy]
        internal struct BasicGetResultStruct
        {
            /// <summary>
            /// Gets the message body of the result
            /// </summary>
            public BodyStruct Body;

            /// <summary>
            /// Gets the message properties
            /// </summary>
            public IBasicProperties BasicProperties;
        }

        internal interface IBasicProperties
        {
            /// <summary>
            /// Gets or sets the headers of the message
            /// </summary>
            /// <returns>Message headers</returns>
            IDictionary<string, object> Headers { get; set; }

            /// <summary>
            /// Gets the delivery mode of the message
            /// </summary>
            byte DeliveryMode { get; }

            /// <summary>
            /// Returns true if the DeliveryMode property is present
            /// </summary>
            /// <returns>true if the DeliveryMode property is present</returns>
            bool IsDeliveryModePresent();
        }

        [DuckCopy]
        internal struct BodyStruct
        {
            /// <summary>
            /// Gets the length of the message body
            /// </summary>
            public int Length;
        }
    }
}
