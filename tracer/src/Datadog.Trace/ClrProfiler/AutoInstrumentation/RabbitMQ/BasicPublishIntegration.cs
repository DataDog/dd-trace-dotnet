// <copyright file="BasicPublishIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client BasicPublish calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "RabbitMQ.Client",
        TypeName = "RabbitMQ.Client.Framing.Impl.Model",
        MethodName = "_Private_BasicPublish",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, ClrNames.Bool, RabbitMQConstants.IBasicPropertiesTypeName, ClrNames.Ignore },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = RabbitMQConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BasicPublishIntegration
    {
        private const string Command = "basic.publish";

        private static readonly string?[] DeliveryModeStrings = { null, "1", "2" };

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TBasicProperties">Type of the message properties</typeparam>
        /// <typeparam name="TBody">Type of the message body</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exchange">Name of the exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="mandatory">The mandatory routing flag.</param>
        /// <param name="basicProperties">The message properties.</param>
        /// <param name="body">The message body.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TBasicProperties, TBody>(TTarget instance, string exchange, string routingKey, bool mandatory, TBasicProperties basicProperties, TBody body)
            where TBasicProperties : IBasicProperties, IDuckType
            where TBody : IBody, IDuckType // Versions < 6.0.0: TBody is byte[] // Versions >= 6.0.0: TBody is ReadOnlyMemory<byte>
            where TTarget : IModelBase
        {
            var scope = RabbitMQIntegration.CreateScope(Tracer.Instance, out var tags, Command, spanKind: SpanKinds.Producer, exchange: exchange, routingKey: routingKey, host: instance?.Session?.Connection?.Endpoint?.HostName);

            // Tags is not null if span is not null, but keep analysis happy, as there's no attribute for that
            if (scope != null && tags is not null)
            {
                Tracer.Instance.CurrentTraceSettings.Schema.RemapPeerService(tags);

                string exchangeDisplayName = string.IsNullOrEmpty(exchange) ? "<default>" : exchange;
                string routingKeyDisplayName = string.IsNullOrEmpty(routingKey) ? "<all>" : routingKey.StartsWith("amq.gen-") ? "<generated>" : routingKey;
                scope.Span.ResourceName = $"{Command} {exchangeDisplayName} -> {routingKeyDisplayName}";

                tags.MessageSize = body.Instance != null ? body.Length.ToString() : "0";

                if (basicProperties.Instance != null)
                {
                    if (basicProperties.IsDeliveryModePresent())
                    {
                        tags.DeliveryMode = DeliveryModeStrings[0x3 & basicProperties.DeliveryMode];
                    }

                    // add distributed tracing headers to the message
                    if (basicProperties.Headers == null)
                    {
                        basicProperties.Headers = new Dictionary<string, object>();
                    }

                    SpanContextPropagator.Instance.Inject(scope.Span.Context, basicProperties.Headers, default(ContextPropagation));
                    RabbitMQIntegration.SetDataStreamsCheckpointOnProduce(
                        Tracer.Instance,
                        scope.Span,
                        tags,
                        basicProperties.Headers,
                        body.Instance != null ? body.Length : 0);
                }
            }

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return CallTargetReturn.GetDefault();
        }
    }
}
