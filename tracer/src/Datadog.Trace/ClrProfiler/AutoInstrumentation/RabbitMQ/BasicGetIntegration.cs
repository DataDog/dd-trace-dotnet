// <copyright file="BasicGetIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// RabbitMQ.Client BasicGet calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "RabbitMQ.Client",
        TypeName = "RabbitMQ.Client.Impl.ModelBase",
        MethodName = "BasicGet",
        ReturnTypeName = "RabbitMQ.Client.BasicGetResult",
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.Bool },
        MinimumVersion = "3.6.9",
        MaximumVersion = "6.*.*",
        IntegrationName = RabbitMQConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BasicGetIntegration
    {
        private const string Command = "basic.get";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(BasicGetIntegration));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queue">The queue name of the message</param>
        /// <param name="autoAck">The original autoAck argument</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, string? queue, bool autoAck)
        {
            return new CallTargetState(scope: null, state: queue, startTime: DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResult">Type of the BasicGetResult</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="basicGetResult">BasicGetResult instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        internal static CallTargetReturn<TResult> OnMethodEnd<TTarget, TResult>(TTarget instance, TResult basicGetResult, Exception exception, in CallTargetState state)
            where TResult : IBasicGetResult, IDuckType
        {
            string? queue = (string?)state.State;
            DateTimeOffset? startTime = state.StartTime;

            SpanContext? propagatedContext = null;
            IBasicProperties? basicProperties = null;
            string? messageSize = null;

            if (basicGetResult.Instance != null)
            {
                messageSize = basicGetResult.Body?.Length.ToString();
                var basicPropertiesHeaders = basicGetResult.BasicProperties?.Headers;

                // try to extract propagated context values from headers
                if (basicPropertiesHeaders != null)
                {
                    try
                    {
                        basicProperties = basicGetResult.BasicProperties;
                        propagatedContext = SpanContextPropagator.Instance.Extract(basicPropertiesHeaders, default(ContextPropagation));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error extracting propagated headers.");
                    }
                }
            }

            using (var scope = RabbitMQIntegration.CreateScope(Tracer.Instance, out var tags, Command, parentContext: propagatedContext, spanKind: SpanKinds.Consumer, queue: queue, startTime: startTime))
            {
                if (scope != null)
                {
                    string? queueDisplayName = string.IsNullOrEmpty(queue) || !queue!.StartsWith("amq.gen-") ? queue : "<generated>";
                    scope.Span.ResourceName = $"{Command} {queueDisplayName}";

                    if (tags != null && messageSize != null)
                    {
                        tags.MessageSize = messageSize;
                    }

                    if (exception != null)
                    {
                        scope.Span.SetException(exception);
                    }

                    if (basicProperties != null && tags is not null)
                    {
                        RabbitMQIntegration.SetDataStreamsCheckpointOnConsume(
                            Tracer.Instance,
                            scope.Span,
                            tags,
                            basicProperties.Headers,
                            basicGetResult.Body?.Length ?? 0,
                            basicProperties.Timestamp.UnixTime != 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - basicProperties.Timestamp.UnixTime : 0);
                    }
                }
            }

            return new CallTargetReturn<TResult>(basicGetResult);
        }
    }
}
